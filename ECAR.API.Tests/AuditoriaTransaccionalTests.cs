using System.Text.Json;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Auditoría transaccional de ECARDbContext (PLAN_FASE4_TAREAS §3.3): una prueba por acción,
/// los campos que nunca se guardan, el SecurityStamp y la cadena de hashes. La transacción y el
/// trigger solo existen en SQL Server: se prueban en ECAR.IntegrationTests.
/// </summary>
public class AuditoriaTransaccionalTests
{
    private sealed class ContextoFalso : IContextoAuditoria
    {
        public long? IdUsuario { get; init; } = 7;
        public string Usuario { get; init; } = "Ana Admin (ana@ecar.com)";
        public string? DireccionIp { get; init; } = "10.0.0.5";
        public string? AgenteUsuario { get; init; } = "Pruebas/1.0";
        public string? Motivo { get; set; }
    }

    private static ECARDbContext CrearContexto(IContextoAuditoria? contexto = null, string? nombreBase = null) =>
        new(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(nombreBase ?? Guid.NewGuid().ToString())
            .Options, contexto);

    private static Dictionary<string, JsonElement> Json(string? json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json!)!;

    private static async Task<List<Auditoria>> FilasAsync(ECARDbContext db) =>
        await db.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).ToListAsync();

    private static Equipo NuevoEquipo() => new()
    {
        CodigoInterno = "EQ-01",
        ActivoFijo = "AF-01",
        NombreEquipo = "Balanza analítica",
        Criticidad = "Alta"
    };

    private static Usuario NuevoUsuario() => new()
    {
        Nombre = "Beto Técnico",
        Correo = "beto@ecar.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Clave-de-prueba-1")
    };

    [Fact]
    public async Task Crear_guarda_todos_los_campos_con_el_id_generado_y_el_autor()
    {
        var contexto = new ContextoFalso();
        await using var db = CrearContexto(contexto);
        var equipo = NuevoEquipo();

        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        var fila = Assert.Single(await FilasAsync(db));
        Assert.Equal("Equipos", fila.Tabla);
        Assert.Equal(equipo.IdEquipo, fila.RegistroId);
        Assert.Equal(AuditoriaAcciones.Crear, fila.Accion);
        Assert.Null(fila.ValorAnterior);
        var nuevo = Json(fila.ValorNuevo);
        Assert.Equal("EQ-01", nuevo["CodigoInterno"].GetString());
        Assert.Equal("Balanza analítica", nuevo["NombreEquipo"].GetString());
        Assert.Equal(equipo.IdEquipo, nuevo["IdEquipo"].GetInt64());
        Assert.Contains("analítica", fila.ValorNuevo); // sin escapar los acentos
        Assert.Equal(7, fila.IdUsuario);
        Assert.Equal("Ana Admin (ana@ecar.com)", fila.Usuario);
        Assert.Equal("10.0.0.5", fila.DireccionIp);
        Assert.Equal("Pruebas/1.0", fila.AgenteUsuario);
        Assert.Null(fila.HashAnterior);
        Assert.Equal(CadenaAuditoria.CalcularHash(fila), fila.Hash);
        Assert.Equal(64, fila.Hash.Length);
        Assert.True((DateTime.UtcNow - fila.FechaHora).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Modificar_guarda_solo_los_campos_cambiados_y_encadena_con_la_fila_anterior()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var equipo = NuevoEquipo();
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        equipo.Marca = "Mettler";
        equipo.Criticidad = "Media";
        equipo.NombreEquipo = "Balanza analítica"; // mismo valor: no es un cambio
        await db.SaveChangesAsync();

        var filas = await FilasAsync(db);
        Assert.Equal(2, filas.Count);
        var modificar = filas[1];
        Assert.Equal(AuditoriaAcciones.Modificar, modificar.Accion);
        Assert.Equal(equipo.IdEquipo, modificar.RegistroId);
        var antes = Json(modificar.ValorAnterior);
        var despues = Json(modificar.ValorNuevo);
        Assert.Equal(["Criticidad", "Marca"], antes.Keys.Order());
        Assert.Equal(JsonValueKind.Null, antes["Marca"].ValueKind);
        Assert.Equal("Alta", antes["Criticidad"].GetString());
        Assert.Equal("Mettler", despues["Marca"].GetString());
        Assert.Equal("Media", despues["Criticidad"].GetString());
        Assert.Equal(filas[0].Hash, modificar.HashAnterior);
    }

    [Fact]
    public async Task Guardar_sin_cambios_no_escribe_auditoria()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var equipo = NuevoEquipo();
        db.Equipos.Add(equipo);
        await db.SaveChangesAsync();

        equipo.Criticidad = "Alta"; // el mismo valor
        await db.SaveChangesAsync();
        await db.SaveChangesAsync();

        Assert.Single(await FilasAsync(db));
    }

    [Fact]
    public async Task Desactivar_un_usuario_renueva_su_sello_y_la_auditoria_no_guarda_secretos()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var usuario = NuevoUsuario();
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var selloInicial = usuario.SecurityStamp;
        var hashPassword = usuario.PasswordHash;

        usuario.Activo = false;
        await db.SaveChangesAsync();

        Assert.NotEqual(selloInicial, usuario.SecurityStamp);
        var filas = await FilasAsync(db);
        Assert.Equal([AuditoriaAcciones.Crear, AuditoriaAcciones.Desactivar], filas.Select(f => f.Accion));
        Assert.Equal("(oculta)", Json(filas[0].ValorNuevo)["PasswordHash"].GetString());
        Assert.Equal(["Activo"], Json(filas[1].ValorNuevo).Keys);
        foreach (var fila in filas)
        {
            Assert.DoesNotContain(hashPassword, fila.ValorNuevo ?? string.Empty);
            Assert.DoesNotContain("SecurityStamp", (fila.ValorAnterior ?? string.Empty) + fila.ValorNuevo);
        }
    }

    [Fact]
    public async Task Reactivar_registra_Activar_y_no_cambia_el_sello()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var usuario = NuevoUsuario();
        usuario.Activo = false;
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var sello = usuario.SecurityStamp;

        usuario.Activo = true;
        await db.SaveChangesAsync();

        Assert.Equal(sello, usuario.SecurityStamp);
        Assert.Equal(AuditoriaAcciones.Activar, (await FilasAsync(db))[1].Accion);
    }

    [Fact]
    public async Task Cambiar_la_contrasena_renueva_el_sello_y_se_audita_como_cambiada()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var usuario = NuevoUsuario();
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var sello = usuario.SecurityStamp;

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Otra-clave-de-prueba-2");
        await db.SaveChangesAsync();

        Assert.NotEqual(sello, usuario.SecurityStamp);
        var fila = (await FilasAsync(db))[1];
        Assert.Equal(AuditoriaAcciones.Modificar, fila.Accion);
        Assert.Equal("(oculta)", Json(fila.ValorAnterior)["PasswordHash"].GetString());
        Assert.Equal("(cambiada)", Json(fila.ValorNuevo)["PasswordHash"].GetString());
    }

    private static async Task<(ECARDbContext Db, ContextoFalso Contexto, Inspeccion Inspeccion, Hallazgo Hallazgo)> InspeccionConHallazgoAsync()
    {
        var contexto = new ContextoFalso();
        var db = CrearContexto(contexto);
        var usuario = NuevoUsuario();
        var equipo = NuevoEquipo();
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0" };
        db.AddRange(usuario, equipo, checklist);
        await db.SaveChangesAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();
        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = "Fuga en la junta",
            Criticidad = "Alta",
            IdUsuarioRegistro = usuario.IdUsuario
        };
        db.Hallazgos.Add(hallazgo);
        await db.SaveChangesAsync();
        return (db, contexto, inspeccion, hallazgo);
    }

    [Fact]
    public async Task Anular_un_hallazgo_registra_Anular_con_el_motivo()
    {
        var (db, contexto, _, hallazgo) = await InspeccionConHallazgoAsync();
        await using var _db = db;

        contexto.Motivo = "  Registrado por error en otro equipo  ";
        hallazgo.Estado = HallazgoEstados.Anulado;
        hallazgo.MotivoAnulacion = "Registrado por error en otro equipo";
        await db.SaveChangesAsync();

        var fila = (await FilasAsync(db)).Last();
        Assert.Equal("Hallazgos", fila.Tabla);
        Assert.Equal(hallazgo.IdHallazgo, fila.RegistroId);
        Assert.Equal(AuditoriaAcciones.Anular, fila.Accion);
        Assert.Equal("Registrado por error en otro equipo", fila.Motivo);
        Assert.Equal(HallazgoEstados.Abierto, Json(fila.ValorAnterior)["Estado"].GetString());
    }

    [Fact]
    public async Task Cerrar_y_reabrir_un_hallazgo_tienen_su_propia_accion()
    {
        var (db, _, _, hallazgo) = await InspeccionConHallazgoAsync();
        await using var _db = db;

        hallazgo.Estado = HallazgoEstados.Cerrado;
        hallazgo.AccionCorrectiva = "Se cambió la junta";
        await db.SaveChangesAsync();
        hallazgo.Estado = HallazgoEstados.Abierto;
        await db.SaveChangesAsync();

        var acciones = (await FilasAsync(db)).TakeLast(2).Select(f => f.Accion);
        Assert.Equal([AuditoriaAcciones.Cerrar, AuditoriaAcciones.Reabrir], acciones);
    }

    [Fact]
    public async Task Firmar_y_anular_una_inspeccion_y_la_firma_se_guarda_como_huella()
    {
        var (db, _, inspeccion, _) = await InspeccionConHallazgoAsync();
        await using var _db = db;
        var png = "iVBORw0KGgo" + new string('A', 400);

        inspeccion.Estado = InspeccionEstados.Cerrada;
        inspeccion.FirmaDigital = png;
        await db.SaveChangesAsync();

        var firmar = (await FilasAsync(db)).Last();
        Assert.Equal(AuditoriaAcciones.Firmar, firmar.Accion);
        var firma = Json(firmar.ValorNuevo)["FirmaDigital"].GetString()!;
        Assert.StartsWith("sha256:", firma);
        Assert.DoesNotContain(png, firmar.ValorNuevo);

        var otra = new Inspeccion
        {
            IdEquipo = inspeccion.IdEquipo,
            IdUsuario = inspeccion.IdUsuario,
            IdChecklist = inspeccion.IdChecklist,
            FechaInspeccion = DateTime.UtcNow
        };
        db.Inspecciones.Add(otra);
        await db.SaveChangesAsync();
        otra.Estado = InspeccionEstados.Anulada;
        await db.SaveChangesAsync();
        Assert.Equal(AuditoriaAcciones.Anular, (await FilasAsync(db)).Last().Accion);
    }

    [Fact]
    public async Task Retirar_una_evidencia_registra_Retirar()
    {
        var (db, _, inspeccion, _) = await InspeccionConHallazgoAsync();
        await using var _db = db;
        var evidencia = new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = "2026/10/1/foto.jpg",
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            IdUsuarioCarga = inspeccion.IdUsuario
        };
        db.Evidencias.Add(evidencia);
        await db.SaveChangesAsync();

        evidencia.Retirada = true;
        evidencia.FechaRetiro = DateTime.UtcNow;
        await db.SaveChangesAsync();

        Assert.Equal(AuditoriaAcciones.Retirar, (await FilasAsync(db)).Last().Accion);
    }

    [Fact]
    public async Task Eliminar_guarda_el_registro_completo_en_el_valor_anterior()
    {
        await using var db = CrearContexto(new ContextoFalso());
        var usuario = NuevoUsuario();
        var rol = new Rol { Nombre = "Auditor" };
        db.AddRange(usuario, rol);
        await db.SaveChangesAsync();
        var asignacion = new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol };
        db.UsuarioRoles.Add(asignacion);
        await db.SaveChangesAsync();
        var idAsignacion = asignacion.Id;

        db.UsuarioRoles.Remove(asignacion);
        await db.SaveChangesAsync();

        var fila = (await FilasAsync(db)).Last();
        Assert.Equal("UsuarioRol", fila.Tabla);
        Assert.Equal(idAsignacion, fila.RegistroId);
        Assert.Equal(AuditoriaAcciones.Eliminar, fila.Accion);
        Assert.Null(fila.ValorNuevo);
        var antes = Json(fila.ValorAnterior);
        Assert.Equal(usuario.IdUsuario, antes["IdUsuario"].GetInt64());
        Assert.Equal(rol.IdRol, antes["IdRol"].GetInt64());
    }

    [Fact]
    public async Task Un_evento_de_sistema_se_escribe_con_el_usuario_indicado()
    {
        await using var db = CrearContexto(new ContextoFalso { IdUsuario = null, Usuario = "Anónimo" });

        await db.RegistrarEventoAsync(AuditoriaAcciones.LoginFallido, 0,
            new { Identidad = "nadie@ecar.com" }, usuario: "nadie@ecar.com");

        var fila = Assert.Single(await FilasAsync(db));
        Assert.Equal(AuditoriaTablas.Sistema, fila.Tabla);
        Assert.Equal(AuditoriaAcciones.LoginFallido, fila.Accion);
        Assert.Equal(0, fila.RegistroId);
        Assert.Null(fila.IdUsuario);
        Assert.Equal("nadie@ecar.com", fila.Usuario);
        Assert.Equal("nadie@ecar.com", Json(fila.ValorNuevo)["Identidad"].GetString());
        Assert.Equal("10.0.0.5", fila.DireccionIp);
    }

    [Fact]
    public async Task Sin_contexto_de_peticion_el_autor_es_Sistema()
    {
        await using var db = CrearContexto();

        db.Roles.Add(new Rol { Nombre = "Técnico" });
        await db.SaveChangesAsync();

        var fila = Assert.Single(await FilasAsync(db));
        Assert.Equal("Sistema", fila.Usuario);
        Assert.Null(fila.IdUsuario);
        Assert.Null(fila.DireccionIp);
    }

    [Fact]
    public async Task Varias_entidades_en_un_guardado_quedan_en_cadena_y_la_verificacion_detecta_una_fila_alterada()
    {
        var nombreBase = Guid.NewGuid().ToString();
        await using (var db = CrearContexto(new ContextoFalso(), nombreBase))
        {
            db.AddRange(NuevoEquipo(), new Rol { Nombre = "Auditor" }, new CategoriaEquipo { Nombre = "Lab" });
            await db.SaveChangesAsync();
            db.Ubicaciones.Add(new Ubicacion { Planta = "Principal", Area = "QC" });
            await db.SaveChangesAsync();
        }

        await using var lectura = CrearContexto(nombreBase: nombreBase);
        var filas = await lectura.Auditoria.OrderBy(a => a.IdAuditoria).ToListAsync();
        Assert.Equal(4, filas.Count);
        Assert.All(filas.Skip(1).Zip(filas), par => Assert.Equal(par.Second.Hash, par.First.HashAnterior));
        Assert.Equal(new ResultadoCadena(true, 4, null, filas[^1].Hash), CadenaAuditoria.Verificar(filas, null));

        // Lotes: el segundo empieza con el último hash del primero.
        var primerLote = CadenaAuditoria.Verificar(filas.Take(2), null);
        Assert.True(CadenaAuditoria.Verificar(filas.Skip(2), primerLote.UltimoHash).Valida);

        // Quitar una fila rompe la siguiente; alterar un valor rompe esa fila.
        var sinUna = filas.Where((_, i) => i != 1).ToList();
        Assert.Equal(filas[2].IdAuditoria, CadenaAuditoria.Verificar(sinUna, null).PrimeraFilaRota);

        var alterada = filas.FindIndex(f => f.Tabla == "CategoriasEquipo");
        filas[alterada].ValorNuevo = filas[alterada].ValorNuevo!.Replace("Lab", "Laboratorio");
        var resultado = CadenaAuditoria.Verificar(filas, null);
        Assert.False(resultado.Valida);
        Assert.Equal(filas[alterada].IdAuditoria, resultado.PrimeraFilaRota);
        Assert.Equal(alterada + 1, resultado.FilasRevisadas);
    }
}
