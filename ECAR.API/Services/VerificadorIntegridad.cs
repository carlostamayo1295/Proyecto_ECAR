using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Entities;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services;

public class VerificadorIntegridad(IOptions<FirmaOptions> opciones, IEvidenciaStorage storage)
{
    public const string AlgoritmoLegado = "SHA256-v0";
    public const string AlgoritmoHmac = "HMACSHA256-v1";

    /// <summary>
    /// Igual que <see cref="AlgoritmoHmac"/> pero sin el resultado: una inspección firmada con
    /// novedades queda "Pendiente" y pasa a "ConNovedad" cuando se resuelve la última, sin que eso
    /// altere lo que se firmó. El resultado sale de las respuestas, que sí están selladas.
    /// </summary>
    public const string AlgoritmoHmacV2 = "HMACSHA256-v2";

    /// <summary>
    /// Sello de la resolución de una novedad. Su texto empieza por "registro=novedad", así que
    /// nunca coincide con el de una inspección aunque use la misma clave.
    /// </summary>
    public const string AlgoritmoNovedad = "HMACSHA256-v1";

    public const string SignificadoEjecucion = "Responsable de la ejecución de la inspección";
    public const string SignificadoResolucion = "Responsable de la resolución de la novedad";

    private const string ArchivoAusente = "ausente";

    public static string? NombreAlgoritmo(Inspeccion inspeccion)
    {
        if (string.IsNullOrEmpty(inspeccion.FirmaHash))
        {
            return null;
        }

        return string.IsNullOrEmpty(inspeccion.FirmaAlgoritmo) ||
               string.Equals(inspeccion.FirmaAlgoritmo, "SHA256", StringComparison.OrdinalIgnoreCase)
            ? AlgoritmoLegado
            : inspeccion.FirmaAlgoritmo;
    }

    /// <summary>El sello con que se firma hoy una inspección (<see cref="AlgoritmoHmacV2"/>).</summary>
    public Task<string> CalcularSelloAsync(Inspeccion inspeccion) => CalcularSelloAsync(inspeccion, AlgoritmoHmacV2);

    public async Task<bool> VerificarAsync(Inspeccion inspeccion)
    {
        if (string.IsNullOrEmpty(inspeccion.FirmaHash) || inspeccion.FechaCierre is not { } fechaCierre ||
            string.IsNullOrEmpty(inspeccion.FirmaDigital))
        {
            return false;
        }

        string esperado;
        switch (NombreAlgoritmo(inspeccion))
        {
            case AlgoritmoLegado:
                esperado = FirmaInspeccion.CalcularHash(inspeccion, inspeccion.Respuestas, inspeccion.Evidencias,
                    fechaCierre, inspeccion.FirmaDigital);
                break;
            case AlgoritmoHmac:
                esperado = await CalcularSelloAsync(inspeccion, AlgoritmoHmac);
                break;
            case AlgoritmoHmacV2:
                esperado = await CalcularSelloAsync(inspeccion, AlgoritmoHmacV2);
                break;
            default:
                return false;
        }

        return Iguales(esperado, inspeccion.FirmaHash);
    }

    /// <summary>
    /// El texto que se sella. <see cref="AlgoritmoHmac"/> incluye el resultado y se conserva para
    /// verificar las inspecciones firmadas antes de las novedades; <see cref="AlgoritmoHmacV2"/> no.
    /// </summary>
    public async Task<string> ConstruirContenidoCanonicoAsync(Inspeccion inspeccion, string version = AlgoritmoHmacV2)
    {
        if (version is not (AlgoritmoHmac or AlgoritmoHmacV2))
        {
            throw new ArgumentException($"Versión de sello desconocida: {version}", nameof(version));
        }

        var fechaCierre = inspeccion.FechaCierre
            ?? throw new InvalidOperationException("La inspección no tiene fecha de cierre");

        var constructor = new StringBuilder();
        constructor
            .Append("version=").Append(version).Append('|')
            .Append("idInspeccion=").Append(inspeccion.IdInspeccion).Append('|')
            .Append("idEquipo=").Append(inspeccion.IdEquipo).Append('|')
            .Append("idChecklist=").Append(inspeccion.IdChecklist).Append('|')
            .Append("idUsuario=").Append(inspeccion.IdUsuario).Append('|')
            .Append("fechaInspeccion=").Append(Fecha(inspeccion.FechaInspeccion)).Append('|')
            .Append("fechaCierre=").Append(Fecha(fechaCierre)).Append('|');
        if (version == AlgoritmoHmac)
        {
            constructor.Append("resultado=").Append(FirmaInspeccion.Escapar(inspeccion.Resultado)).Append('|');
        }

        constructor
            .Append("observaciones=").Append(FirmaInspeccion.Escapar(inspeccion.Observaciones)).Append('|')
            .Append("firmaNombre=").Append(FirmaInspeccion.Escapar(inspeccion.FirmaNombre)).Append('|')
            .Append("firmaSignificado=").Append(FirmaInspeccion.Escapar(inspeccion.FirmaSignificado)).Append('|')
            .Append("respuestas=[");

        foreach (var respuesta in inspeccion.Respuestas.OrderBy(r => r.IdPregunta))
        {
            constructor
                .Append('{').Append(respuesta.IdPregunta).Append(';')
                .Append(FirmaInspeccion.Escapar(respuesta.Respuesta)).Append(';')
                .Append(FirmaInspeccion.Escapar(respuesta.Observacion)).Append('}');
        }

        constructor.Append("]|evidencias=[");
        // Las retiradas no forman parte del registro firmado (§3.4, endpoint 11): retirar una foto
        // antes de firmar no cambia el sello, y la que se retiró no puede volver a contar.
        foreach (var evidencia in inspeccion.Evidencias.Where(e => !e.Retirada).OrderBy(e => e.IdEvidencia))
        {
            constructor
                .Append('{').Append(evidencia.IdEvidencia).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.Archivo)).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.NombreOriginal)).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.TipoContenido)).Append(';')
                .Append(evidencia.TamanoBytes).Append(';')
                .Append(Fecha(evidencia.FechaCarga)).Append(';')
                .Append(evidencia.IdUsuarioCarga).Append(';')
                .Append(await HashArchivoAsync(evidencia.Archivo)).Append('}');
        }

        constructor.Append("]|firma=").Append(inspeccion.FirmaDigital);
        return constructor.ToString();
    }

    /// <summary>
    /// Sello de la resolución de una novedad: lo que se reportó, lo que se hizo, quién y cuándo lo
    /// firmó, la firma manuscrita y el sello de la inspección de la que salió, que la ata a ese
    /// registro firmado. Necesita <see cref="Novedad.Inspeccion"/> cargada.
    /// </summary>
    public string CalcularSelloNovedad(Novedad novedad) => Hmac(ConstruirContenidoCanonicoNovedad(novedad));

    public bool VerificarNovedad(Novedad novedad)
    {
        if (string.IsNullOrEmpty(novedad.FirmaHash) || novedad.FechaResolucion is null ||
            string.IsNullOrEmpty(novedad.FirmaDigital) || novedad.FirmaAlgoritmo != AlgoritmoNovedad)
        {
            return false;
        }

        return Iguales(CalcularSelloNovedad(novedad), novedad.FirmaHash);
    }

    public static string ConstruirContenidoCanonicoNovedad(Novedad novedad)
    {
        var inspeccion = novedad.Inspeccion
            ?? throw new InvalidOperationException("La novedad no tiene su inspección cargada");
        var fechaResolucion = novedad.FechaResolucion
            ?? throw new InvalidOperationException("La novedad no tiene fecha de resolución");

        return new StringBuilder()
            .Append("registro=novedad|")
            .Append("version=").Append(AlgoritmoNovedad).Append('|')
            .Append("idNovedad=").Append(novedad.IdNovedad).Append('|')
            .Append("idInspeccion=").Append(novedad.IdInspeccion).Append('|')
            .Append("selloInspeccion=").Append(FirmaInspeccion.Escapar(inspeccion.FirmaHash)).Append('|')
            .Append("idPregunta=").Append(novedad.IdPregunta).Append('|')
            .Append("idRespuesta=").Append(novedad.IdRespuesta).Append('|')
            .Append("descripcion=").Append(FirmaInspeccion.Escapar(novedad.Descripcion)).Append('|')
            .Append("criticidad=").Append(FirmaInspeccion.Escapar(novedad.Criticidad)).Append('|')
            .Append("fechaRegistro=").Append(Fecha(novedad.FechaRegistro)).Append('|')
            .Append("idUsuarioRegistro=").Append(novedad.IdUsuarioRegistro).Append('|')
            .Append("solucion=").Append(FirmaInspeccion.Escapar(novedad.Solucion)).Append('|')
            .Append("fechaResolucion=").Append(Fecha(fechaResolucion)).Append('|')
            .Append("idUsuarioResolucion=").Append(novedad.IdUsuarioResolucion).Append('|')
            .Append("firmaNombre=").Append(FirmaInspeccion.Escapar(novedad.FirmaNombre)).Append('|')
            .Append("firmaSignificado=").Append(FirmaInspeccion.Escapar(novedad.FirmaSignificado)).Append('|')
            .Append("firma=").Append(novedad.FirmaDigital)
            .ToString();
    }

    private async Task<string> CalcularSelloAsync(Inspeccion inspeccion, string version) =>
        Hmac(await ConstruirContenidoCanonicoAsync(inspeccion, version));

    private string Hmac(string canonico)
    {
        var hmac = HMACSHA256.HashData(ObtenerClave(), Encoding.UTF8.GetBytes(canonico));
        return Convert.ToHexString(hmac).ToLowerInvariant();
    }

    private static bool Iguales(string esperado, string guardado) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(esperado),
            Encoding.ASCII.GetBytes(guardado.ToLowerInvariant()));

    private byte[] ObtenerClave()
    {
        var clave = opciones.Value.ClaveHmac;
        if (string.IsNullOrWhiteSpace(clave) || clave.Length < FirmaOptions.LongitudMinimaClave)
        {
            throw new InvalidOperationException(
                $"Firma:ClaveHmac no está configurada o tiene menos de {FirmaOptions.LongitudMinimaClave} caracteres");
        }

        return Encoding.UTF8.GetBytes(clave);
    }

    private async Task<string> HashArchivoAsync(string? archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo))
        {
            return ArchivoAusente;
        }

        try
        {
            await using var contenido = await storage.AbrirAsync(archivo);
            var hash = await SHA256.HashDataAsync(contenido);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ArchivoAusente;
        }
    }

    private static string Fecha(DateTime fecha) => fecha.Ticks.ToString(CultureInfo.InvariantCulture);
}
