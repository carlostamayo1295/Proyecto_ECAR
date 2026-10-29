using ECAR.Shared.DTOs;

namespace ECAR.API.Configuration;

public class SeguridadOptions
{
    public const string SectionName = "Seguridad";

    public int LongitudMinimaPassword { get; set; } = 10;
    public bool RequiereLetrasYNumeros { get; set; } = true;
    /// <summary>
    /// 0: la contraseña no caduca (decisión de ECAR, 29/10/2026; antes 90 días). Con un valor mayor que
    /// 0 vuelve la caducidad: <see cref="Services.PoliticaPasswords.EstaCaducada"/> obliga a cambiarla.
    /// </summary>
    public int DiasExpiracionPassword { get; set; }

    public int PasswordsRecordadas { get; set; } = 5;

    public int MaxIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 30;
    public int MinutosInactividad { get; set; } = 15;

    public PoliticaSeguridadDto ToDto() => new()
    {
        LongitudMinimaPassword = LongitudMinimaPassword,
        RequiereLetrasYNumeros = RequiereLetrasYNumeros,
        DiasExpiracionPassword = DiasExpiracionPassword,
        PasswordsRecordadas = PasswordsRecordadas,
        MaxIntentosFallidos = MaxIntentosFallidos,
        MinutosBloqueo = MinutosBloqueo,
        MinutosInactividad = MinutosInactividad
    };
}

public class FirmaOptions
{
    public const string SectionName = "Firma";

    public const int LongitudMinimaClave = 32;

    public string ClaveHmac { get; set; } = string.Empty;
}
