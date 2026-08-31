namespace ECAR.API.Services;

public sealed class EcarAuthenticationOptions
{
    public const string SectionName = "ECARAuthentication";

    /// <summary>
    /// Supported values: Local, ActiveDirectory and Hybrid.
    /// ActiveDirectory and Hybrid require a configured IActiveDirectoryAuthService.
    /// </summary>
    public string Mode { get; set; } = nameof(EcarAuthenticationMode.Local);

    public EcarAuthenticationMode GetMode()
    {
        return Enum.TryParse<EcarAuthenticationMode>(Mode, ignoreCase: true, out var mode)
            ? mode
            : throw new InvalidOperationException(
                $"Unsupported authentication mode '{Mode}'. Use Local, ActiveDirectory or Hybrid.");
    }
}

public enum EcarAuthenticationMode
{
    Local,
    ActiveDirectory,
    Hybrid
}
