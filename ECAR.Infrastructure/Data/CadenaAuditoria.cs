using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Fila = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.Infrastructure.Data;

/// <summary>
/// Cadena de integridad de la auditoría (PLAN_FASE4_TAREAS §3.3): cada fila guarda el hash de la
/// anterior y su propio SHA-256, así que modificar o borrar una fila rompe la cadena desde ahí.
/// ECARDbContext la escribe; GET /api/auditoria/verificar (BE-1) la recorre con <see cref="Verificar"/>.
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
}

/// <param name="Valida">Todas las filas revisadas están intactas y encadenadas.</param>
/// <param name="FilasRevisadas">Incluye la fila rota, si la hay.</param>
/// <param name="PrimeraFilaRota">IdAuditoria de la primera fila que no cuadra.</param>
/// <param name="UltimoHash">Hash de la última fila buena, para continuar con el lote siguiente.</param>
public sealed record ResultadoCadena(bool Valida, int FilasRevisadas, long? PrimeraFilaRota, string? UltimoHash);
