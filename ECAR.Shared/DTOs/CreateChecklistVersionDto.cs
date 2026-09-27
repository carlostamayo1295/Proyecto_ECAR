using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateChecklistVersionDto
{
    [Required(ErrorMessage = "La versión es requerida")]
    [MaxLength(20, ErrorMessage = "La versión no puede exceder 20 caracteres")]
    public string Version { get; set; } = string.Empty;
}
