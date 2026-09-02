namespace ECAR.Shared.DTOs;

public class CreateRespuestaInspeccionDto
{
    public long IdInspeccion { get; set; }
    public long IdPregunta { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}