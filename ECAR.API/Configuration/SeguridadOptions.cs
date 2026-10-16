using ECAR.Shared.DTOs;

namespace ECAR.API.Configuration;

/// <summary>
/// Sección "Seguridad" de appsettings (PLAN_FASE4_TAREAS §3.2 y §3.8, Parte 11 §11.300).
/// Los valores por defecto son los del plan; la política la publica GET /api/auth/politica.
/// </summary>
public class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    public int MaxIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 30;
    public int LongitudMinimaPassword { get; set; } = 10;
    public int DiasExpiracionPassword { get; set; } = 90;
    public int PasswordsRecordadas { get; set; } = 5;
    public int MinutosInactividad { get; set; } = 15;

    public static SeguridadOptions Desde(IConfiguration configuration) =>
        configuration.GetSection(SectionName).Get<SeguridadOptions>() ?? new SeguridadOptions();

    public PoliticaSeguridadDto ComoPolitica() => new()
    {
        LongitudMinimaPassword = LongitudMinimaPassword,
        RequiereLetrasYNumeros = true,
        DiasExpiracionPassword = DiasExpiracionPassword,
        PasswordsRecordadas = PasswordsRecordadas,
        MaxIntentosFallidos = MaxIntentosFallidos,
        MinutosBloqueo = MinutosBloqueo,
        MinutosInactividad = MinutosInactividad
    };
}

/// <summary>
/// Sección "Firma" (PLAN_FASE4_TAREAS §3.2 y §3.7, Parte 11 §11.50 y §11.70). La clave HMAC va en
/// user-secrets en desarrollo y en el almacén de secretos del servidor en producción, nunca en
/// git ni en la base: sin ella, quien tenga acceso a SQL podría alterar y volver a sellar.
/// </summary>
public class FirmaOptions
{
    public const string SectionName = "Firma";

    /// <summary>32 bytes o más en base64 (dotnet user-secrets set "Firma:ClaveHmac" "...").</summary>
    public string ClaveHmac { get; set; } = string.Empty;

    /// <summary>Significado de la firma (§11.50(a)(3)); pendiente de confirmar el texto con ECAR.</summary>
    public string Significado { get; set; } = "Responsable de la ejecución de la inspección";
}
