namespace ECAR.Shared.DTOs;

public class ChecklistVersionDto
{
    public long IdChecklist { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int TotalPreguntas { get; set; }

    // Un checklist con respuestas registradas ya no admite cambios en sus preguntas.
    public bool TieneRespuestas { get; set; }
}
