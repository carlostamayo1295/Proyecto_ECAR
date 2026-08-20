using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateUsuarioDto
{
    [Required(ErrorMessage = "El nombre es requerido")]
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es requerido")]
    [EmailAddress(ErrorMessage = "Formato de correo inválido")]
    [MaxLength(150, ErrorMessage = "El correo no puede exceder 150 caracteres")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El password es requerido")]
    [MinLength(6, ErrorMessage = "El password debe tener al menos 6 caracteres")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "El usuario AD no puede exceder 100 caracteres")]
    public string? UsuarioAD { get; set; }

    public List<long>? RoleIds { get; set; }
}