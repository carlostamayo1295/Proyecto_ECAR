namespace ECAR.Shared.DTOs;

// Respuesta pública de la consulta por QR: solo ficha del equipo y checklists aplicables.
public class ConsultaQrDto
{
    public EquipoDto Equipo { get; set; } = new();
    public List<ChecklistDto> ChecklistsActivos { get; set; } = new();
}
