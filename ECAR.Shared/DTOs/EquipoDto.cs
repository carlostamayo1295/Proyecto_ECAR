namespace ECAR.Shared.DTOs;

public class EquipoDto
{
    public long IdEquipo { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string ActivoFijo { get; set; } = string.Empty;
    public string? SerialFabricante { get; set; }
    public string NombreEquipo { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public string? Fabricante { get; set; }
    public string? Criticidad { get; set; }
    public long? IdCategoria { get; set; }
    public string? CategoriaNombre { get; set; }
    public long? IdUbicacion { get; set; }
    public string? UbicacionNombre { get; set; }
    public string? QRCode { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
