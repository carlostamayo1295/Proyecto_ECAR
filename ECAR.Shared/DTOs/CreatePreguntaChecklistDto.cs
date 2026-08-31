using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreatePreguntaChecklistDto
{
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un checklist válido")]
    public long IdChecklist { get; set; }

    [Required(ErrorMessage = "La pregunta es requerida")]
    public string Pregunta { get; set; } = string.Empty;

    [Required(ErrorMessage = "El tipo de respuesta es requerido")]
    [MaxLength(50, ErrorMessage = "El tipo de respuesta no puede exceder 50 caracteres")]
    public string TipoRespuesta { get; set; } = string.Empty;

    public bool Obligatoria { get; set; }
}
