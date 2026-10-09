using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ECAR.Infrastructure.Entities;

namespace ECAR.API.Services;

/// <summary>
/// Reglas de la firma de cierre (§3.4 del plan de Fase 3): validación del PNG manuscrito y
/// cálculo del sello de integridad. Vive fuera del controlador para que el hash pueda
/// recalcularse desde una auditoría sin pasar por HTTP.
/// </summary>
public static class FirmaInspeccion
{
    /// <summary>Tamaño máximo del PNG ya decodificado (200 KB), igual que el límite del DTO.</summary>
    public const int TamanoMaximoBytes = 200 * 1024;

    /// <summary>Cabecera obligatoria de todo archivo PNG (RFC 2083).</summary>
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private const string PrefijoDataUrl = "data:image/png;base64,";

    /// <summary>
    /// Comprueba que el texto recibido sea un PNG real en base64. No basta con mirar la
    /// extensión o el prefijo del data URL: se decodifica y se verifican los magic bytes.
    /// </summary>
    /// <param name="firmaBase64">Contenido enviado por el cliente, con o sin prefijo data URL.</param>
    /// <param name="normalizada">Base64 limpio (sin prefijo) listo para guardar.</param>
    /// <param name="error">Motivo del rechazo, en el idioma del usuario.</param>
    public static bool TryValidarPng(string? firmaBase64, out string normalizada, out string error)
    {
        normalizada = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(firmaBase64))
        {
            error = "La firma es requerida";
            return false;
        }

        var contenido = firmaBase64.Trim();
        if (contenido.StartsWith(PrefijoDataUrl, StringComparison.OrdinalIgnoreCase))
        {
            contenido = contenido[PrefijoDataUrl.Length..];
        }

        byte[] binario;
        try
        {
            binario = Convert.FromBase64String(contenido);
        }
        catch (FormatException)
        {
            error = "La firma no es un contenido base64 válido";
            return false;
        }

        if (binario.Length > TamanoMaximoBytes)
        {
            error = "La firma no puede exceder 200 KB";
            return false;
        }

        if (binario.Length < FirmaPng.Length || !binario.AsSpan(0, FirmaPng.Length).SequenceEqual(FirmaPng))
        {
            error = "La firma debe ser una imagen PNG";
            return false;
        }

        normalizada = contenido;
        return true;
    }

    /// <summary>
    /// Sello SHA-256 del contenido cerrado. Se calcula sobre el texto canónico de
    /// <see cref="ConstruirContenidoCanonico"/>: si después alguien altera una respuesta,
    /// una evidencia o la firma, el hash recalculado deja de coincidir.
    /// </summary>
    public static string CalcularHash(
        Inspeccion inspeccion,
        IEnumerable<RespuestaInspeccion> respuestas,
        IEnumerable<Evidencia> evidencias,
        DateTime fechaCierre,
        string firmaPngBase64)
    {
        var canonico = ConstruirContenidoCanonico(
            inspeccion, respuestas, evidencias, fechaCierre, firmaPngBase64);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonico));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Texto canónico que se firma. El orden es fijo (respuestas por IdPregunta, evidencias por
    /// IdEvidencia) y los valores viajan escapados, para que dos cálculos del mismo cierre den
    /// siempre el mismo resultado independientemente del orden en que la base los devuelva.
    /// </summary>
    public static string ConstruirContenidoCanonico(
        Inspeccion inspeccion,
        IEnumerable<RespuestaInspeccion> respuestas,
        IEnumerable<Evidencia> evidencias,
        DateTime fechaCierre,
        string firmaPngBase64)
    {
        var constructor = new StringBuilder();
        constructor
            .Append("idInspeccion=").Append(inspeccion.IdInspeccion).Append('|')
            .Append("idEquipo=").Append(inspeccion.IdEquipo).Append('|')
            .Append("idChecklist=").Append(inspeccion.IdChecklist).Append('|')
            .Append("idUsuario=").Append(inspeccion.IdUsuario).Append('|')
            .Append("fechaCierre=")
            .Append(fechaCierre.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
            .Append('|')
            .Append("respuestas=[");

        foreach (var respuesta in respuestas.OrderBy(r => r.IdPregunta))
        {
            constructor
                .Append('{').Append(respuesta.IdPregunta).Append(';')
                .Append(Escapar(respuesta.Respuesta)).Append(';')
                .Append(Escapar(respuesta.Observacion)).Append('}');
        }

        constructor.Append("]|").Append("evidencias=[");
        foreach (var evidencia in evidencias.OrderBy(e => e.IdEvidencia))
        {
            constructor.Append('{').Append(evidencia.IdEvidencia).Append('}');
        }

        constructor.Append("]|").Append("firma=").Append(firmaPngBase64);
        return constructor.ToString();
    }

    /// <summary>Neutraliza los separadores para que dos campos distintos no produzcan el mismo texto.</summary>
    private static string Escapar(string? valor) => (valor ?? string.Empty)
        .Replace(@"\", @"\\", StringComparison.Ordinal)
        .Replace(";", @"\;", StringComparison.Ordinal)
        .Replace("|", @"\|", StringComparison.Ordinal)
        .Replace("{", @"\{", StringComparison.Ordinal)
        .Replace("}", @"\}", StringComparison.Ordinal);
}
