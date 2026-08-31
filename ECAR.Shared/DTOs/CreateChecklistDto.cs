using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateChecklistDto
{
    [Required(ErrorMessage = "El nombre del checklist es requerido")]
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La versión es requerida")]
    [MaxLength(20, ErrorMessage = "La versión no puede exceder 20 caracteres")]
    public string Version { get; set; } = string.Empty;

    public List<CreatePreguntaChecklistDto>? Preguntas { get; set; }
}
