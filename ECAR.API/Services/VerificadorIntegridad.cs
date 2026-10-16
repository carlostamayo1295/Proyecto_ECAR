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
    public const string SignificadoEjecucion = "Responsable de la ejecución de la inspección";

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

    public async Task<string> CalcularSelloAsync(Inspeccion inspeccion)
    {
        var canonico = await ConstruirContenidoCanonicoAsync(inspeccion);
        var hmac = HMACSHA256.HashData(ObtenerClave(), Encoding.UTF8.GetBytes(canonico));
        return Convert.ToHexString(hmac).ToLowerInvariant();
    }

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
                esperado = await CalcularSelloAsync(inspeccion);
                break;
            default:
                return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(esperado),
            Encoding.ASCII.GetBytes(inspeccion.FirmaHash.ToLowerInvariant()));
    }

    public async Task<string> ConstruirContenidoCanonicoAsync(Inspeccion inspeccion)
    {
        var fechaCierre = inspeccion.FechaCierre
            ?? throw new InvalidOperationException("La inspección no tiene fecha de cierre");

        var constructor = new StringBuilder();
        constructor
            .Append("version=").Append(AlgoritmoHmac).Append('|')
            .Append("idInspeccion=").Append(inspeccion.IdInspeccion).Append('|')
            .Append("idEquipo=").Append(inspeccion.IdEquipo).Append('|')
            .Append("idChecklist=").Append(inspeccion.IdChecklist).Append('|')
            .Append("idUsuario=").Append(inspeccion.IdUsuario).Append('|')
            .Append("fechaInspeccion=").Append(Fecha(inspeccion.FechaInspeccion)).Append('|')
            .Append("fechaCierre=").Append(Fecha(fechaCierre)).Append('|')
            .Append("resultado=").Append(FirmaInspeccion.Escapar(inspeccion.Resultado)).Append('|')
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
        foreach (var evidencia in inspeccion.Evidencias.OrderBy(e => e.IdEvidencia))
        {
            constructor
                .Append('{').Append(evidencia.IdEvidencia).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.Archivo)).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.NombreOriginal)).Append(';')
                .Append(FirmaInspeccion.Escapar(evidencia.TipoContenido)).Append(';')
                .Append(evidencia.TamanoBytes).Append(';')
                .Append(Fecha(evidencia.FechaCarga)).Append(';')
                .Append(evidencia.IdUsuarioCarga).Append(';')
                .Append(evidencia.Retirada ? '1' : '0').Append(';')
                .Append(await HashArchivoAsync(evidencia.Archivo)).Append('}');
        }

        constructor.Append("]|firma=").Append(inspeccion.FirmaDigital);
        return constructor.ToString();
    }

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
