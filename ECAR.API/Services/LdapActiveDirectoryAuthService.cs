using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using System.Net;

namespace ECAR.API.Services;

public sealed class LdapActiveDirectoryAuthService : IActiveDirectoryAuthService
{
    private readonly ActiveDirectoryOptions _options;
    private readonly ILogger<LdapActiveDirectoryAuthService> _logger;

    public LdapActiveDirectoryAuthService(
        IOptions<ActiveDirectoryOptions> options,
        ILogger<LdapActiveDirectoryAuthService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> AuthenticateAsync(
        string usuarioAd,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(usuarioAd) || string.IsNullOrEmpty(password))
            return false;

        try
        {
            var identifier = new LdapDirectoryIdentifier(
                _options.Server,
                _options.Port,
                fullyQualifiedDnsHostName: true,
                connectionless: false);
            using var connection = new LdapConnection(identifier)
            {
                AuthType = AuthType.Negotiate,
                Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 1, 60))
            };
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = _options.UseSsl;

            var credential = CreateCredential(usuarioAd.Trim(), password);
            await Task.Run(() => connection.Bind(credential), cancellationToken);
            return true;
        }
        catch (LdapException exception)
        {
            // Never log the password or reveal whether the credentials or the server failed.
            _logger.LogWarning(exception, "Falló la autenticación de directorio para {UsuarioAD}", usuarioAd);
            return false;
        }
    }

    private NetworkCredential CreateCredential(string usuarioAd, string password)
    {
        if (usuarioAd.Contains('@') || usuarioAd.Contains('\\') || string.IsNullOrWhiteSpace(_options.Domain))
            return new NetworkCredential(usuarioAd, password);

        return new NetworkCredential(usuarioAd, password, _options.Domain.Trim());
    }
}
