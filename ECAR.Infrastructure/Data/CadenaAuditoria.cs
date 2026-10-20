using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Fila = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.Infrastructure.Data;

/// <summary>
/// Cadena de integridad de la auditoría (PLAN_FASE4_TAREAS §3.3): cada fila guarda el hash de la
/// anterior y su propio SHA-256, así que modificar o borrar una fila rompe la cadena desde ahí.
/// ECARDbContext la escribe; GET /api/auditoria/verificar la recorre con <see cref="VerificarTablaAsync"/>.
/// </summary>
/// <remarks>
/// La migración Fase4HallazgosAuditoria sella en T-SQL las filas anteriores con esta misma fórmula
/// (HASHBYTES sobre nvarchar = UTF-16LE y FORMAT con el mismo patrón de fecha). Si se cambia algo
/// aquí, hay que cambiarlo allí y en las filas ya escritas no se puede: la fórmula es definitiva.
/// </remarks>
public static class CadenaAuditoria
{
    /// <summary>Fecha con los 7 decimales de datetime2; sin zona, porque SQL Server la devuelve sin ella.</summary>
    public const string FormatoFecha = "yyyy-MM-dd'T'HH':'mm':'ss'.'fffffff";

    /// <summary>
    /// HashAnterior | Tabla | RegistroId | Accion | ValorAnterior | ValorNuevo | IdUsuario |
    /// FechaHora | Motivo, con los nulos como texto vacío.
    /// </summary>
    public static string Contenido(Fila fila) => string.Join('|',
        fila.HashAnterior ?? string.Empty,
        fila.Tabla,
        fila.RegistroId.ToString(CultureInfo.InvariantCulture),
        fila.Accion,
        fila.ValorAnterior ?? string.Empty,
        fila.ValorNuevo ?? string.Empty,
        fila.IdUsuario?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        fila.FechaHora.ToString(FormatoFecha, CultureInfo.InvariantCulture),
        fila.Motivo ?? string.Empty);

    /// <summary>SHA-256 del contenido en UTF-16LE, en hexadecimal en mayúsculas (64 caracteres).</summary>
    public static string CalcularHash(Fila fila) =>
        Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(Contenido(fila))));

    /// <summary>
    /// Recorre <paramref name="filasEnOrden"/> (ordenadas por IdAuditoria) y comprueba que cada una
    /// apunta a la anterior y que su hash corresponde a su contenido. Para revisar por lotes, se
    /// pasa como <paramref name="hashAnteriorEsperado"/> el <see cref="ResultadoCadena.UltimoHash"/>
    /// del lote anterior; en el primer lote de la tabla es null.
    /// </summary>
    public static ResultadoCadena Verificar(IEnumerable<Fila> filasEnOrden, string? hashAnteriorEsperado)
    {
        var revisadas = 0;
        var esperado = hashAnteriorEsperado;
        foreach (var fila in filasEnOrden)
        {
            revisadas++;
            if (!string.Equals(fila.HashAnterior, esperado, StringComparison.Ordinal) ||
                !string.Equals(fila.Hash, CalcularHash(fila), StringComparison.Ordinal))
            {
                return new ResultadoCadena(false, revisadas, fila.IdAuditoria, esperado);
            }

            esperado = fila.Hash;
        }

        return new ResultadoCadena(true, revisadas, null, esperado);
    }

    /// <summary>
    /// Recorre la tabla Auditoria en orden de IdAuditoria, por lotes de <paramref name="tamanoLote"/>
    /// filas, con <see cref="Verificar"/>, y se detiene en la primera fila rota. Sin fechas revisa la
    /// tabla entera desde la primera fila (HashAnterior null).
    /// </summary>
    /// <remarks>
    /// Con <paramref name="desdeUtc"/> o <paramref name="hastaUtcExclusivo"/> revisa un tramo: del
    /// primer IdAuditoria con FechaHora en el periodo al último, incluidas todas las filas
    /// intermedias (aunque un desfase de milisegundos entre dos guardados simultáneos deje la hora
    /// de alguna fuera), para no abrir huecos en la cadena. La primera fila del tramo se ancla al
    /// Hash guardado de la fila anterior: el tramo prueba que no se alteró nada dentro de él, y
    /// solo la verificación completa prueba la tabla entera.
    /// </remarks>
    public static async Task<ResultadoCadena> VerificarTablaAsync(ECARDbContext db, DateTime? desdeUtc = null,
        DateTime? hastaUtcExclusivo = null, int tamanoLote = 1000, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanoLote, 1);
        var filas = db.Auditoria.AsNoTracking();

        long? primerId = null;
        if (desdeUtc.HasValue)
        {
            primerId = await filas.Where(a => a.FechaHora >= desdeUtc.Value)
                .OrderBy(a => a.IdAuditoria).Select(a => (long?)a.IdAuditoria).FirstOrDefaultAsync(cancellationToken);
            if (primerId == null)
            {
                return new ResultadoCadena(true, 0, null, null);
            }
        }

        long? ultimoId = null;
        if (hastaUtcExclusivo.HasValue)
        {
            ultimoId = await filas.Where(a => a.FechaHora < hastaUtcExclusivo.Value)
                .OrderByDescending(a => a.IdAuditoria).Select(a => (long?)a.IdAuditoria).FirstOrDefaultAsync(cancellationToken);
            if (ultimoId == null || ultimoId < primerId)
            {
                return new ResultadoCadena(true, 0, null, null);
            }
        }

        // El hash que debe traer la primera fila del tramo: el de la fila anterior, o null al
        // empezar por el principio de la tabla.
        string? esperado = null;
        var desdeId = primerId ?? long.MinValue;
        if (primerId.HasValue)
        {
            esperado = await filas.Where(a => a.IdAuditoria < desdeId)
                .OrderByDescending(a => a.IdAuditoria).Select(a => a.Hash).FirstOrDefaultAsync(cancellationToken);
        }

        var hastaId = ultimoId ?? long.MaxValue;
        var revisadas = 0;
        long? ultimaFila = null;
        while (true)
        {
            var lote = await filas.Where(a => a.IdAuditoria >= desdeId && a.IdAuditoria <= hastaId)
                .OrderBy(a => a.IdAuditoria)
                .Take(tamanoLote)
                .ToListAsync(cancellationToken);
            if (lote.Count == 0)
            {
                break;
            }

            var resultado = Verificar(lote, esperado);
            revisadas += resultado.FilasRevisadas;
            if (!resultado.Valida)
            {
                return resultado with { FilasRevisadas = revisadas };
            }

            esperado = resultado.UltimoHash;
            ultimaFila = lote[^1].IdAuditoria;
            if (lote.Count < tamanoLote)
            {
                break;
            }

            desdeId = lote[^1].IdAuditoria + 1;
        }

        return new ResultadoCadena(true, revisadas, null, esperado) { UltimaFila = ultimaFila };
    }

    /// <summary>
    /// Ancla externa contra truncados: true si la fila <paramref name="idAuditoria"/> sigue en la
    /// tabla con el mismo <paramref name="hash"/> que tenía cuando el auditor la anotó. La cadena
    /// sola no ve que falten las últimas filas (no queda ninguna después que lo delate), y un ancla
    /// guardada en la propia tabla se borraría con ellas: por eso el ancla se conserva fuera.
    /// </summary>
    public static Task<bool> AnclaIntactaAsync(ECARDbContext db, long idAuditoria, string hash,
        CancellationToken cancellationToken = default)
    {
        var normalizado = hash.Trim().ToUpperInvariant();
        return db.Auditoria.AsNoTracking()
            .AnyAsync(a => a.IdAuditoria == idAuditoria && a.Hash == normalizado, cancellationToken);
    }
}

/// <param name="Valida">Todas las filas revisadas están intactas y encadenadas.</param>
/// <param name="FilasRevisadas">Incluye la fila rota, si la hay.</param>
/// <param name="PrimeraFilaRota">IdAuditoria de la primera fila que no cuadra.</param>
/// <param name="UltimoHash">Hash de la última fila buena, para continuar con el lote siguiente.</param>
public sealed record ResultadoCadena(bool Valida, int FilasRevisadas, long? PrimeraFilaRota, string? UltimoHash)
{
    /// <summary>
    /// IdAuditoria de la última fila revisada de una cadena válida (junto con <see cref="UltimoHash"/>,
    /// el ancla que el auditor guarda fuera de la base). Solo la rellena
    /// <see cref="CadenaAuditoria.VerificarTablaAsync"/>; null si la cadena está rota o no hubo filas.
    /// </summary>
    public long? UltimaFila { get; init; }
}
