using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateRolDto
{
    [Required(ErrorMessage = "El nombre del rol es requerido")]
    [MaxLength(50, ErrorMessage = "El nombre no puede exceder 50 caracteres")]
    public string Nombre { get; set; } = string.Empty;
}