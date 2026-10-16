using System.Security.Cryptography;
using System.Text;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Entities;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services;

/// <summary>
/// Firma electrónica de la Fase 4 (PLAN_FASE4_TAREAS §3.7, Parte 11 §11.50 y §11.70): sella la
/// inspección con HMAC-SHA256 y una clave que no está en la base, de modo que alterar el registro
/// en SQL y recalcular la huella ya no basta para ocultarlo.
/// </summary>
public interface IFirmaElectronica
{
    /// <summary>Significado que se guarda con cada firma (§11.50(a)(3)).</summary>
    string Significado { get; }

    /// <summary>Huella de una inspección que se está cerrando, con el algoritmo vigente.</summary>
    string CalcularHuella(Inspeccion inspeccion, IEnumerable<RespuestaInspeccion> respuestas,
        IEnumerable<Evidencia> evidencias, DateTime fechaCierre, string firmaPngBase64,
        string firmaNombre, string firmaSignificado);

    /// <summary>Recalcula la huella de una inspección cerrada con el algoritmo con que se firmó.</summary>
    bool EsIntegra(Inspeccion inspeccion);
}

public sealed class FirmaElectronica : IFirmaElectronica
{
    private readonly byte[]? _clave;
    private readonly FirmaOptions _opciones;

    public FirmaElectronica(IOptions<FirmaOptions> opciones)
    {
        _opciones = opciones.Value;
        _clave = LeerClave(_opciones.ClaveHmac);
    }

    public string Significado => _opciones.Significado;

    public string CalcularHuella(Inspeccion inspeccion, IEnumerable<RespuestaInspeccion> respuestas,
        IEnumerable<Evidencia> evidencias, DateTime fechaCierre, string firmaPngBase64,
        string firmaNombre, string firmaSignificado) =>
        FirmaInspeccion.CalcularHmac(inspeccion, respuestas, evidencias, fechaCierre, firmaPngBase64,
            firmaNombre, firmaSignificado, Clave());

    public bool EsIntegra(Inspeccion inspeccion)
    {
        if (string.IsNullOrEmpty(inspeccion.FirmaHash) || string.IsNullOrEmpty(inspeccion.FirmaDigital) ||
            inspeccion.FechaCierre is not { } fechaCierre)
        {
            return false;
        }

        var recalculada = FirmaInspeccion.AlgoritmoDe(inspeccion) switch
        {
            FirmaInspeccion.AlgoritmoHmacV1 => FirmaInspeccion.CalcularHmac(inspeccion, inspeccion.Respuestas,
                inspeccion.Evidencias, fechaCierre, inspeccion.FirmaDigital, inspeccion.FirmaNombre ?? string.Empty,
                inspeccion.FirmaSignificado ?? string.Empty, Clave()),
            // Cerradas antes de la Fase 4: SHA-256 sin clave sobre todas sus evidencias.
            FirmaInspeccion.AlgoritmoSha256V0 => FirmaInspeccion.CalcularHash(inspeccion, inspeccion.Respuestas,
                inspeccion.Evidencias, fechaCierre, inspeccion.FirmaDigital),
            _ => null
        };

        return recalculada is not null && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(recalculada), Encoding.ASCII.GetBytes(inspeccion.FirmaHash));
    }

    private byte[] Clave() => _clave ?? throw new InvalidOperationException(
        "Falta la clave de firma: configure Firma:ClaveHmac (32 bytes en base64) en user-secrets " +
        "o en el almacén de secretos del servidor");

    private static byte[]? LeerClave(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            var clave = Convert.FromBase64String(base64.Trim());
            return clave.Length >= 32 ? clave : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
