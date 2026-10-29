using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ECAR.API.Tests;

internal static class FirmaPruebas
{
    public const string Password = "FirmaSegura2026";
    public const string ClaveHmac = "clave-hmac-solo-para-pruebas-con-mas-de-32-caracteres";
    public const string JwtSecret = "una-clave-secreta-de-prueba-suficientemente-larga-32b";

    public static readonly string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, 4);

    public static AuthService CrearAuthService(ECARDbContext context, SeguridadOptions? seguridad = null) =>
        new(context,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = JwtSecret,
                ["JWT:Issuer"] = "ECAR-Pruebas",
                ["JWT:Audience"] = "ECAR-Pruebas"
            }).Build(),
            new SinActiveDirectory(),
            Options.Create(new EcarAuthenticationOptions { Mode = nameof(EcarAuthenticationMode.Local) }),
            new PoliticaPasswords(Options.Create(seguridad ?? new SeguridadOptions())));

    public static VerificadorIntegridad CrearVerificador(IEvidenciaStorage storage, string clave = ClaveHmac) =>
        new(Options.Create(new FirmaOptions { ClaveHmac = clave }), storage);

    /// <summary>
    /// Firma como lo haría la pantalla: con la contraseña de pruebas y, si la inspección no tiene sede, con
    /// <paramref name="sede"/> (obligatoria desde el 29/10/2026). Con <paramref name="sede"/> null no la pone.
    /// </summary>
    public static async Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarConPassword(
        this InspeccionesController controller,
        ECARDbContext context,
        long id,
        FirmarInspeccionDto dto,
        IEvidenciaStorage? storage = null,
        string? sede = Sedes.Rionegro)
    {
        dto.Password ??= Password;
        if (sede != null && await context.Inspecciones.FindAsync(id) is { Sede: null } inspeccion)
        {
            inspeccion.Sede = sede;
            await context.SaveChangesAsync();
        }

        return await controller.FirmarInspeccion(id, dto, CrearAuthService(context),
            CrearVerificador(storage ?? new AlmacenVacio()));
    }

    private sealed class SinActiveDirectory : IActiveDirectoryAuthService
    {
        public Task<bool> AuthenticateAsync(string usuarioAd, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class AlmacenVacio : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"pruebas/{idInspeccion}{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }
}
