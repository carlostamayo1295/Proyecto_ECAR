using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECAR.API.Tests;

/// <summary>
/// Dependencias de la Fase 4 con valores de prueba, para que cada archivo de pruebas construya los
/// controladores igual: firma HMAC con una clave fija, la política por defecto y un verificador de
/// contraseñas que acepta <see cref="ClaveFirma"/>.
/// </summary>
internal static class Fabrica
{
    /// <summary>La contraseña que el verificador falso da por buena al firmar.</summary>
    public const string ClaveFirma = "Clave-de-firma-1";

    /// <summary>32 bytes fijos: las huellas son reproducibles entre pruebas.</summary>
    public static readonly string ClaveHmac = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    public static IFirmaElectronica Firma(string? claveHmac = null) => new FirmaElectronica(Options.Create(new FirmaOptions
    {
        ClaveHmac = claveHmac ?? ClaveHmac,
        Significado = "Responsable de la ejecución de la inspección"
    }));

    public static IOptions<SeguridadOptions> Seguridad() => Options.Create(new SeguridadOptions());

    public static InspeccionesController Inspecciones(ECARDbContext context, ICurrentUser usuario,
        IEvidenciaStorage storage, IAuthService? auth = null, IMotivoCambio? motivo = null) =>
        new(context, usuario, new InspeccionService(context), storage, NullLogger<InspeccionesController>.Instance,
            auth ?? new AuthFalso(), Firma(), new HallazgoService(context), motivo ?? new MotivoCambio());

    public static UsuariosController Usuarios(ECARDbContext context) => new(context, new MotivoCambio(), Seguridad());

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

    /// <summary>
    /// Verificador de contraseñas para las pruebas de firma: <see cref="ClaveFirma"/> es correcta,
    /// "bloqueada" simula una cuenta bloqueada y cualquier otra es incorrecta.
    /// </summary>
    public sealed class AuthFalso : IAuthService
    {
        public List<string> Contextos { get; } = new();

        public Task<ResultadoCredencial> VerificarPasswordAsync(long idUsuario, string? password, string contexto)
        {
            Contextos.Add(contexto);
            return Task.FromResult(password switch
            {
                ClaveFirma => new ResultadoCredencial(EstadoCredencial.Valida, Local: true),
                "bloqueada" => new ResultadoCredencial(EstadoCredencial.Bloqueada, DateTime.UtcNow.AddMinutes(30)),
                _ => new ResultadoCredencial(EstadoCredencial.Invalida)
            });
        }

        public Task<LoginResponseDto?> LoginAsync(LoginDto loginDto) => Task.FromResult<LoginResponseDto?>(null);

        public Task<ResultadoLogin> IniciarSesionAsync(LoginDto loginDto) =>
            Task.FromResult(new ResultadoLogin(EstadoCredencial.Invalida, null, null));

        public Task<bool> ValidateTokenAsync(string token) => Task.FromResult(false);
    }
}
