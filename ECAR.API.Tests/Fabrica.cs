using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECAR.API.Tests;

/// <summary>
/// Controladores de la Fase 4 con dependencias de prueba, para que cada archivo los construya igual.
/// La firma con contraseña y la huella HMAC usan <see cref="FirmaPruebas"/> (BE-3).
/// </summary>
internal static class Fabrica
{
    /// <summary>La contraseña con que firman los usuarios sembrados con <see cref="FirmaPruebas.PasswordHash"/>.</summary>
    public const string ClaveFirma = FirmaPruebas.Password;

    public static IOptions<SeguridadOptions> Seguridad() => Options.Create(new SeguridadOptions());

    public static InspeccionesController Inspecciones(ECARDbContext context, ICurrentUser usuario,
        IEvidenciaStorage storage, IMotivoCambio? motivo = null) =>
        new(context, usuario, new InspeccionService(context), storage, NullLogger<InspeccionesController>.Instance,
            motivo ?? new MotivoCambio());

    public static UsuariosController Usuarios(ECARDbContext context, IMotivoCambio? motivo = null) =>
        new(context, new PoliticaPasswords(Seguridad()), motivo ?? new MotivoCambio());

    /// <summary>
    /// El pie de página de la primera hoja de un .xlsx, tal como quedó en el XML (ClosedXML no lo
    /// vuelve a leer al abrir el archivo).
    /// </summary>
    public static string PieDeExcel(byte[] xlsx)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(xlsx));
        using var lector = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = System.Xml.Linq.XDocument.Load(lector);
        return string.Concat(xml.Descendants().Where(e => e.Name.LocalName == "oddFooter").Select(e => e.Value));
    }
}
