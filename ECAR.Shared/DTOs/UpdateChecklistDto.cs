using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class UpdateChecklistDto
{
    [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
    public string? Nombre { get; set; }

    [MaxLength(20, ErrorMessage = "La versión no puede exceder 20 caracteres")]
    public string? Version { get; set; }

    public bool? Activo { get; set; }

    /// <summary>Fase 4 (§3.3): obligatorio al modificar un checklist; queda en la auditoría.</summary>
    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string? Motivo { get; set; }

    public List<CreatePreguntaChecklistDto>? Preguntas { get; set; }
}
