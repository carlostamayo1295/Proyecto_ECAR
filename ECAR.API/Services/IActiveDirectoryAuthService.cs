namespace ECAR.API.Services;

/// <summary>
/// Contract for validating credentials against the ECAR corporate directory.
/// </summary>
public interface IActiveDirectoryAuthService
{
    Task<bool> AuthenticateAsync(
        string usuarioAd,
        string password,
        CancellationToken cancellationToken = default);
}
