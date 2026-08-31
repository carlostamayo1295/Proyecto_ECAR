using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class UpdateChecklistDto
{
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? Nombre { get; set; }

    [MaxLength(20, ErrorMessage = "La versión no puede exceder 20 caracteres")]
    public string? Version { get; set; }

    public bool? Activo { get; set; }

    public List<CreatePreguntaChecklistDto>? Preguntas { get; set; }
}
