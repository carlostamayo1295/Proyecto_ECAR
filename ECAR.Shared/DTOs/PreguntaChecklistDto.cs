namespace ECAR.Shared.DTOs;

public class PreguntaChecklistDto
{
    public long IdPregunta { get; set; }
    public long IdChecklist { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string TipoRespuesta { get; set; } = string.Empty;
    public bool Obligatoria { get; set; }
}
