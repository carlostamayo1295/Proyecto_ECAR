namespace ECAR.Shared.DTOs;

public class ChecklistDto
{
    public long IdChecklist { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<PreguntaChecklistDto> Preguntas { get; set; } = new();
}
