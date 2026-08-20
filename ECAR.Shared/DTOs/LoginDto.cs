using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "El correo o usuario AD es requerido")]
    public string CorreoOrUsuarioAD { get; set; } = string.Empty;

    [Required(ErrorMessage = "El password es requerido")]
    public string Password { get; set; } = string.Empty;
}