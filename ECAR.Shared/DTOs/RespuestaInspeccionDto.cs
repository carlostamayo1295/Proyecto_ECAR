namespace ECAR.Shared.DTOs;

public class RespuestaInspeccionDto
{
    public long IdRespuesta { get; set; }
    public long IdInspeccion { get; set; }
    public long IdPregunta { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}