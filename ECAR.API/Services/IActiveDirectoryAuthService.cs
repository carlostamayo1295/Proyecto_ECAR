namespace ECAR.API.Services;

/// <summary>
/// Contrato para validar credenciales contra el directorio corporativo de ECAR.
/// </summary>
public interface IActiveDirectoryAuthService
{
    Task<bool> AuthenticateAsync(
        string usuarioAd,
        string password,
        CancellationToken cancellationToken = default);
}
