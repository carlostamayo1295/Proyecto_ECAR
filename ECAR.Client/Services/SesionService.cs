using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ECAR.Client.Services;

/// <summary>
/// Vigencia de la sesión y vuelta al login. Es el único sitio que decide que una sesión
/// terminó y el único que manda al login conservando la página en la que estaba el usuario,
/// para que después de iniciar sesión vuelva ahí.
///
/// No depende de AuthService a propósito: AuthService necesita el HttpClient, y el HttpClient
/// necesita este servicio para su <see cref="SesionExpiradaHandler"/>. Lee y borra el token
/// directamente en localStorage, con la misma clave.
/// </summary>
public class SesionService
{
    private const string TokenKey = "auth_token";

    // Margen para no lanzar una petición con un token que caduca mientras viaja.
    private static readonly TimeSpan Margen = TimeSpan.FromSeconds(5);

    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigation;
    private bool _redirigiendo;

    public SesionService(IJSRuntime jsRuntime, NavigationManager navigation)
    {
        _jsRuntime = jsRuntime;
        _navigation = navigation;
    }

    /// <summary>Instante en que vence el token guardado; null si no hay token o no se puede leer.</summary>
    public async Task<DateTimeOffset?> ObtenerExpiracionAsync()
    {
        var token = await LeerTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            // ValidTo es DateTime.MinValue si el token no trae "exp": se trata como vencido.
            var validoHasta = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            return new DateTimeOffset(DateTime.SpecifyKind(validoHasta, DateTimeKind.Utc));
        }
        catch
        {
            // Un token ilegible se trata como vencido.
            return DateTimeOffset.MinValue;
        }
    }

    public async Task<bool> HayTokenAsync() => !string.IsNullOrEmpty(await LeerTokenAsync());

    /// <summary>true si hay token y no ha vencido.</summary>
    public async Task<bool> TieneSesionVigenteAsync()
    {
        var expiracion = await ObtenerExpiracionAsync();
        return expiracion.HasValue && expiracion.Value - Margen > DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Manda al login conservando la página actual. Si <paramref name="expirada"/> es true,
    /// borra el token y el login avisa de que la sesión expiró. Con <paramref name="porInactividad"/>
    /// borra el token y el aviso dice que se cerró por inactividad (Fase 4, PLAN §3.8).
    /// </summary>
    public async Task IrAlLoginAsync(bool expirada, bool porInactividad = false)
    {
        if (_redirigiendo || EsPaginaPublica())
        {
            return;
        }

        _redirigiendo = true;

        if (expirada || porInactividad)
        {
            await BorrarTokenAsync();
        }

        var destino = $"/login?returnUrl={Uri.EscapeDataString(PaginaActual())}";
        if (porInactividad)
        {
            destino += "&inactividad=1";
        }
        else if (expirada)
        {
            destino += "&expirada=1";
        }

        // forceLoad reinicia la aplicación: el menú y los roles se vuelven a leer sin token.
        _navigation.NavigateTo(destino, forceLoad: true);
    }

    /// <summary>
    /// Páginas a las que se puede estar sin sesión: el login y la consulta pública del QR.
    /// Desde ellas nunca se redirige al login.
    /// </summary>
    public bool EsPaginaPublica()
    {
        var ruta = PaginaActual();
        return ruta.StartsWith("login", StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWith("equipos/qr/", StringComparison.OrdinalIgnoreCase);
    }

    private string PaginaActual() => _navigation.ToBaseRelativePath(_navigation.Uri);

    private async Task<string?> LeerTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    private async Task BorrarTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
        catch
        {
            // Si no se puede borrar, el login lo sobrescribirá al iniciar sesión.
        }
    }
}
