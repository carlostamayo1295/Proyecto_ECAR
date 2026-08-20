using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class UpdateUsuarioDto
{
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? Nombre { get; set; }

    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    [MaxLength(150, ErrorMessage = "El correo no puede exceder 150 caracteres")]
    public string? Correo { get; set; }

    public string? UsuarioAD { get; set; }

    public bool? Activo { get; set; }

    [MinLength(6, ErrorMessage = "El password debe tener al menos 6 caracteres")]
    public string? Password { get; set; }

    public List<long>? RoleIds { get; set; }
}