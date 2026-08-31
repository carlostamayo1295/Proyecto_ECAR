namespace ECAR.Shared.DTOs;

public class UsuarioDto
{
    public long IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string? UsuarioAD { get; set; }
    public bool Activo { get; set; }
    public List<string>? Roles { get; set; }
}