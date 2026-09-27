using System.Security.Claims;

namespace ECAR.API.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No hay un contexto HTTP disponible");

    public long IdUsuario
    {
        get
        {
            var value = Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out var idUsuario)
                ? idUsuario
                : throw new InvalidOperationException("El token no contiene un identificador de usuario válido");
        }
    }

    public string Nombre => Principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public IReadOnlyCollection<string> Roles => Principal.FindAll(ClaimTypes.Role)
        .Select(claim => claim.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool IsInRole(string role) => Principal.IsInRole(role);
}
