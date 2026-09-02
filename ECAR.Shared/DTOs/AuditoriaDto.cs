namespace ECAR.Shared.DTOs;

public class AuditoriaDto
{
    public long IdAuditoria { get; set; }
    public string Tabla { get; set; } = string.Empty;
    public long RegistroId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
}
