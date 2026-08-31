namespace ECAR.Shared.DTOs;

public class UbicacionDto
{
    public long IdUbicacion { get; set; }
    public string Planta { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}