using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using FilaAuditoria = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.Infrastructure.Data;

/// <summary>
/// Auditoría transaccional (PLAN_FASE4_TAREAS §3.3, Parte 11 §11.10(e)). Es el único punto de
/// escritura de la tabla Auditoria: cada SaveChanges guarda los cambios y sus filas de auditoría
/// en la misma transacción, así que no puede quedar un cambio sin rastro ni un rastro sin cambio.
/// </summary>
/// <remarks>
/// ExecuteUpdate y ExecuteDelete no pasan por aquí: no se usan sobre tablas auditadas.
/// Si algún día se activa EnableRetryOnFailure, este método debe ejecutarse dentro de la
/// estrategia de reintento (CreateExecutionStrategy), porque abre su propia transacción.
/// </remarks>
public partial class ECARDbContext
{
    public const string TriggerAuditoriaSoloInsercion = "TR_Auditoria_SoloInsercion";

    private const string Sistema = "Sistema";

    // Nunca se guardan: el sello invalida tokens y UltimoAcceso cambia en cada inicio de sesión.
    private static readonly HashSet<string> CamposExcluidos = [nameof(Usuario.SecurityStamp), nameof(Usuario.UltimoAcceso)];

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        // Acentos legibles en la base y en el visor, en lugar de ó.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IContextoAuditoria? _contextoAuditoria;
    private readonly List<FilaAuditoria> _eventosPendientes = [];

    /// <summary>
    /// Deja un evento sin entidad (inicio de sesión, exportación, verificación…) para el próximo
    /// SaveChanges, con Tabla = "Sistema". <paramref name="registroId"/> es el id del usuario
    /// afectado (0 si no existe). <paramref name="usuario"/> e <paramref name="idUsuario"/>
    /// sustituyen a los de la petición, p. ej. en un inicio de sesión, donde aún no hay token.
    /// Nunca se pasa una contraseña en <paramref name="detalle"/>.
    /// </summary>
    public void RegistrarEvento(string accion, long registroId = 0, object? detalle = null,
        string? usuario = null, long? idUsuario = null)
    {
        _eventosPendientes.Add(new FilaAuditoria
        {
            Tabla = AuditoriaTablas.Sistema,
            RegistroId = registroId,
            Accion = accion,
            ValorNuevo = detalle is null ? null : JsonSerializer.Serialize(detalle, OpcionesJson),
            Usuario = usuario ?? string.Empty,
            IdUsuario = idUsuario
        });
    }

    /// <summary><see cref="RegistrarEvento"/> y guardar en el momento.</summary>
    public async Task RegistrarEventoAsync(string accion, long registroId = 0, object? detalle = null,
        string? usuario = null, long? idUsuario = null, CancellationToken cancellationToken = default)
    {
        RegistrarEvento(accion, registroId, detalle, usuario, idUsuario);
        await SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        if (!acceptAllChangesOnSuccess)
        {
            // Los cambios tienen que quedar confirmados antes de guardar sus filas de auditoría.
            throw new NotSupportedException("ECARDbContext no admite acceptAllChangesOnSuccess = false");
        }

        ChangeTracker.DetectChanges();
        RenovarSecurityStamps();
        var cambios = CapturarCambios();
        if (cambios.Count == 0 && _eventosPendientes.Count == 0)
        {
            return await base.SaveChangesAsync(true, cancellationToken);
        }

        // Si quien llama ya abrió una transacción, la auditoría entra en ella y la confirma él.
        var transaccion = Database.IsRelational() && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var filas = await base.SaveChangesAsync(true, cancellationToken);
            await EscribirAuditoriaAsync(cambios, cancellationToken);
            if (transaccion is not null)
            {
                await transaccion.CommitAsync(cancellationToken);
            }

            return filas;
        }
        catch
        {
            if (transaccion is not null)
            {
                await transaccion.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            _eventosPendientes.Clear();
            if (transaccion is not null)
            {
                await transaccion.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Desactivar a un usuario o cambiar su contraseña renueva su SecurityStamp, y con eso sus
    /// tokens anteriores dejan de valer en la siguiente petición (§11.300(c), endpoint 27). Se
    /// hace aquí para que valga para cualquier camino: edición, restablecer, cambiar contraseña.
    /// </summary>
    private void RenovarSecurityStamps()
    {
        foreach (var entrada in ChangeTracker.Entries<Usuario>().Where(e => e.State == EntityState.Modified))
        {
            var password = entrada.Property(u => u.PasswordHash);
            var activo = entrada.Property(u => u.Activo);
            var cambioPassword = password.IsModified && !Equals(password.OriginalValue, password.CurrentValue);
            var desactivado = activo.IsModified && activo.OriginalValue && !activo.CurrentValue;
            if (cambioPassword || desactivado)
            {
                entrada.Property(u => u.SecurityStamp).CurrentValue = Usuario.NuevoSecurityStamp();
            }
        }
    }

    private List<CambioAuditado> CapturarCambios()
    {
        var cambios = new List<CambioAuditado>();
        foreach (var entrada in ChangeTracker.Entries())
        {
            if (entrada.Entity is FilaAuditoria)
            {
                continue;
            }

            switch (entrada.State)
            {
                case EntityState.Added:
                    // Los valores se leen después de guardar, cuando ya tienen su id.
                    cambios.Add(new CambioAuditado(entrada, AuditoriaAcciones.Crear));
                    break;

                case EntityState.Modified:
                    var antes = new Dictionary<string, object?>();
                    var despues = new Dictionary<string, object?>();
                    foreach (var propiedad in entrada.Properties)
                    {
                        if (!propiedad.IsModified || CamposExcluidos.Contains(propiedad.Metadata.Name) ||
                            Equals(propiedad.OriginalValue, propiedad.CurrentValue))
                        {
                            continue;
                        }

                        var nombre = propiedad.Metadata.Name;
                        antes[nombre] = ValorAuditable(nombre, propiedad.OriginalValue, oculto: "(oculta)");
                        despues[nombre] = ValorAuditable(nombre, propiedad.CurrentValue, oculto: "(cambiada)");
                    }

                    if (despues.Count > 0)
                    {
                        cambios.Add(new CambioAuditado(entrada, AccionDeModificacion(entrada.Entity, antes, despues))
                        {
                            Antes = antes,
                            Despues = despues,
                            RegistroId = ClavePrimaria(entrada, original: false)
                        });
                    }

                    break;

                case EntityState.Deleted:
                    cambios.Add(new CambioAuditado(entrada, AuditoriaAcciones.Eliminar)
                    {
                        Antes = TodosLosValores(entrada, original: true),
                        RegistroId = ClavePrimaria(entrada, original: true)
                    });
                    break;
            }
        }

        return cambios;
    }

    private async Task EscribirAuditoriaAsync(List<CambioAuditado> cambios, CancellationToken cancellationToken)
    {
        var fecha = DateTime.UtcNow;
        var filas = new List<FilaAuditoria>(cambios.Count + _eventosPendientes.Count);
        foreach (var cambio in cambios)
        {
            if (cambio.Accion == AuditoriaAcciones.Crear)
            {
                cambio.Despues = TodosLosValores(cambio.Entrada, original: false);
                cambio.RegistroId = ClavePrimaria(cambio.Entrada, original: false);
            }

            filas.Add(new FilaAuditoria
            {
                Tabla = NombreTabla(cambio.Entrada.Metadata),
                RegistroId = cambio.RegistroId,
                Accion = cambio.Accion,
                ValorAnterior = cambio.Antes is null ? null : JsonSerializer.Serialize(cambio.Antes, OpcionesJson),
                ValorNuevo = cambio.Despues is null ? null : JsonSerializer.Serialize(cambio.Despues, OpcionesJson)
            });
        }

        filas.AddRange(_eventosPendientes);

        var idUsuario = _contextoAuditoria?.IdUsuario;
        var usuario = _contextoAuditoria?.Usuario ?? Sistema;
        var motivo = string.IsNullOrWhiteSpace(_contextoAuditoria?.Motivo) ? null : _contextoAuditoria!.Motivo!.Trim();
        var direccionIp = Recortar(_contextoAuditoria?.DireccionIp, 45);
        var agente = Recortar(_contextoAuditoria?.AgenteUsuario, 300);

        // El último hash se lee bloqueado: otra transacción que audite espera a que esta termine,
        // y así dos guardados simultáneos no pueden colgar de la misma fila anterior.
        var hashAnterior = await LeerUltimoHashAsync(cancellationToken);
        foreach (var fila in filas)
        {
            fila.IdUsuario ??= idUsuario;
            fila.Usuario = Recortar(string.IsNullOrEmpty(fila.Usuario) ? usuario : fila.Usuario, 100)!;
            fila.Motivo = motivo;
            fila.DireccionIp = direccionIp;
            fila.AgenteUsuario = agente;
            fila.FechaHora = fecha;
            fila.HashAnterior = hashAnterior;
            fila.Hash = CadenaAuditoria.CalcularHash(fila);

            // Una fila por guardado: en un lote, SQL Server no garantiza que los IdAuditoria sigan
            // el orden de la cadena, y la verificación recorre la tabla por IdAuditoria.
            Auditoria.Add(fila);
            await base.SaveChangesAsync(true, cancellationToken);
            hashAnterior = fila.Hash;
        }
    }

    private async Task<string?> LeerUltimoHashAsync(CancellationToken cancellationToken)
    {
        if (!Database.IsRelational())
        {
            return await Auditoria.AsNoTracking()
                .OrderByDescending(a => a.IdAuditoria)
                .Select(a => a.Hash)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var hashes = await Database
            .SqlQueryRaw<string>("SELECT TOP (1) [Hash] AS [Value] FROM [Auditoria] WITH (UPDLOCK, HOLDLOCK) ORDER BY [IdAuditoria] DESC")
            .ToListAsync(cancellationToken);
        return hashes.FirstOrDefault();
    }

    private static string AccionDeModificacion(object entidad, Dictionary<string, object?> antes,
        Dictionary<string, object?> despues)
    {
        if (despues.TryGetValue("Activo", out var activo) && activo is bool estaActivo)
        {
            return estaActivo ? AuditoriaAcciones.Activar : AuditoriaAcciones.Desactivar;
        }

        if (entidad is Evidencia && despues.TryGetValue(nameof(Evidencia.Retirada), out var retirada) && retirada is true)
        {
            return AuditoriaAcciones.Retirar;
        }

        if (despues.TryGetValue("Estado", out var estado) && estado is string nuevoEstado)
        {
            switch (entidad)
            {
                case Inspeccion when nuevoEstado == InspeccionEstados.Anulada:
                case Hallazgo when nuevoEstado == HallazgoEstados.Anulado:
                    return AuditoriaAcciones.Anular;
                case Inspeccion when nuevoEstado == InspeccionEstados.Cerrada:
                    return AuditoriaAcciones.Firmar;
                case Hallazgo when nuevoEstado == HallazgoEstados.Cerrado:
                    return AuditoriaAcciones.Cerrar;
                case Hallazgo when nuevoEstado == HallazgoEstados.Abierto &&
                                   antes.GetValueOrDefault("Estado") as string == HallazgoEstados.Cerrado:
                    return AuditoriaAcciones.Reabrir;
            }
        }

        return AuditoriaAcciones.Modificar;
    }

    private static Dictionary<string, object?> TodosLosValores(EntityEntry entrada, bool original)
    {
        var valores = new Dictionary<string, object?>();
        foreach (var propiedad in entrada.Properties)
        {
            var nombre = propiedad.Metadata.Name;
            if (!CamposExcluidos.Contains(nombre))
            {
                valores[nombre] = ValorAuditable(nombre,
                    original ? propiedad.OriginalValue : propiedad.CurrentValue, oculto: "(oculta)");
            }
        }

        return valores;
    }

    /// <summary>
    /// Los hashes de contraseña nunca llegan a la auditoría. De la firma (un PNG en base64)
    /// se guarda su SHA-256: identifica la imagen sin copiarla en cada fila.
    /// </summary>
    private static object? ValorAuditable(string propiedad, object? valor, string oculto)
    {
        if (propiedad == nameof(Usuario.PasswordHash))
        {
            return valor is null ? null : oculto;
        }

        if (propiedad == nameof(Inspeccion.FirmaDigital) && valor is string firma)
        {
            return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firma)));
        }

        return valor;
    }

    private static long ClavePrimaria(EntityEntry entrada, bool original)
    {
        var clave = entrada.Metadata.FindPrimaryKey()?.Properties;
        if (clave is not { Count: 1 })
        {
            return 0;
        }

        var propiedad = entrada.Property(clave[0].Name);
        return (original ? propiedad.OriginalValue : propiedad.CurrentValue) switch
        {
            long valor => valor,
            int valor => valor,
            _ => 0
        };
    }

    /// <summary>
    /// El nombre de [Table], que es el de SQL Server: el proveedor en memoria de las pruebas no
    /// aplica las convenciones relacionales y GetTableName devolvería el nombre del DbSet.
    /// </summary>
    private static string NombreTabla(IEntityType tipo) =>
        tipo.ClrType.GetCustomAttribute<TableAttribute>()?.Name ?? tipo.GetTableName() ?? tipo.ClrType.Name;

    private static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrEmpty(texto) || texto.Length <= maximo ? texto : texto[..maximo];

    private sealed class CambioAuditado(EntityEntry entrada, string accion)
    {
        public EntityEntry Entrada { get; } = entrada;
        public string Accion { get; } = accion;
        public long RegistroId { get; set; }
        public Dictionary<string, object?>? Antes { get; init; }
        public Dictionary<string, object?>? Despues { get; set; }
    }
}
