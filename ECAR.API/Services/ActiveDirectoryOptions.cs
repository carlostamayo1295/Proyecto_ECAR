namespace ECAR.API.Services;

public sealed class ActiveDirectoryOptions
{
    public const string SectionName = "ActiveDirectory";

    public bool Enabled { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public string? Domain { get; set; }
    public bool UseSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
}
