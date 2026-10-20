using System.Security.Claims;
using ECAR.Infrastructure.Data;

namespace ECAR.API.Services;

/// <summary>
/// La auditoría (§3.3 y Parte 11 §11.10(e) y (h)) toma de la petición en curso quién hace el
/// cambio, desde qué IP y con qué navegador, y el motivo que fijó el controlador. Fuera de una
/// petición (la siembra al arrancar) el autor es "Sistema".
/// </summary>
public sealed class ContextoAuditoriaHttp(IHttpContextAccessor httpContextAccessor, IMotivoCambio motivoCambio)
    : IContextoAuditoria
{
    private HttpContext? Peticion => httpContextAccessor.HttpContext;

    private ClaimsPrincipal? Autenticado =>
        Peticion?.User.Identity?.IsAuthenticated == true ? Peticion.User : null;

    public long? IdUsuario =>
        long.TryParse(Autenticado?.FindFirstValue(ClaimTypes.NameIdentifier), out var idUsuario) ? idUsuario : null;

    public string Usuario
    {
        get
        {
            if (Peticion is null)
            {
                return "Sistema";
            }

            if (Autenticado is not { } principal)
            {
                return "Anónimo";
            }

            var nombre = principal.FindFirstValue(ClaimTypes.Name);
            var correo = principal.FindFirstValue(ClaimTypes.Email);
            return string.IsNullOrWhiteSpace(correo) ? nombre ?? string.Empty : $"{nombre} ({correo})";
        }
    }

    public string? DireccionIp => Peticion?.Connection.RemoteIpAddress?.ToString();

    public string? AgenteUsuario
    {
        get
        {
            var agente = Peticion?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(agente) ? null : agente;
        }
    }

    public string? Motivo => motivoCambio.Motivo;
}
