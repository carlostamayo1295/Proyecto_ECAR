namespace ECAR.Shared.DTOs;

public class UsuarioDto
{
    public long IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? UsuarioAD { get; set; }
    public bool Activo { get; set; }
    public List<string>? Roles { get; set; }

    // --- Fase 4: seguridad de la cuenta (Parte 11 §11.300(d)) ---

    /// <summary>Hasta cuándo está bloqueada por intentos fallidos (UTC); null o pasada si no lo está.</summary>
    public DateTime? BloqueadoHasta { get; set; }

    /// <summary>Tiene una contraseña temporal o caducada que debe cambiar al entrar.</summary>
    public bool DebeCambiarPassword { get; set; }

    public DateTime? UltimoAcceso { get; set; }
}