using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>POST /api/auth/cambiar-password (endpoint 23; Parte 11 §11.300(b)).</summary>
public class CambiarPasswordDto
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    public string Actual { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    public string Nueva { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/auth/politica (endpoint 24, anónimo). La pantalla de contraseñas muestra estas reglas
/// y el cierre por inactividad usa MinutosInactividad. Los valores por defecto son los del plan
/// (§3.2): el cliente los usa mientras el API no publique el endpoint.
/// </summary>
public class PoliticaSeguridadDto
{
    public int LongitudMinimaPassword { get; set; } = 10;
    public bool RequiereLetrasYNumeros { get; set; } = true;
    public int DiasExpiracionPassword { get; set; } = 90;
    public int PasswordsRecordadas { get; set; } = 5;
    public int MaxIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 30;
    public int MinutosInactividad { get; set; } = 15;
}

/// <summary>
/// POST /api/usuarios/{id}/restablecer-password (endpoint 25). La contraseña temporal se
/// muestra una sola vez al Administrador; el usuario debe cambiarla al entrar.
/// </summary>
public class PasswordTemporalDto
{
    public string PasswordTemporal { get; set; } = string.Empty;
    public bool DebeCambiarAlEntrar { get; set; } = true;
}
