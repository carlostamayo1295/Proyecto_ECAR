namespace ECAR.Shared.DTOs;

public class UsuarioRolDto
{
    public long Id { get; set; }
    public long IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioCorreo { get; set; } = string.Empty;
    public long IdRol { get; set; }
    public string RolNombre { get; set; } = string.Empty;
}
