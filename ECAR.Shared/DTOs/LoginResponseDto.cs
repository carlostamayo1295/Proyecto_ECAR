namespace ECAR.Shared.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime Expiration { get; set; }

    // --- Fase 4 (Parte 11 §11.300(b)) ---

    /// <summary>true tras un restablecimiento o con la contraseña caducada: hay que cambiarla antes de entrar.</summary>
    public bool DebeCambiarPassword { get; set; }

    /// <summary>Cuándo caduca la contraseña local; null para usuarios de Active Directory.</summary>
    public DateTime? PasswordExpiraEl { get; set; }
}