namespace ECAR.Shared.DTOs;

public class EquipoDto
{
    public long IdEquipo { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string ActivoFijo { get; set; } = string.Empty;
    public string NombreEquipo { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Criticidad { get; set; }
    public bool Activo { get; set; }
}
