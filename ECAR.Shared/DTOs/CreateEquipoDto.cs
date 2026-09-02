using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateEquipoDto
{
    [Required(ErrorMessage = "El código interno es requerido")]
    [MaxLength(50, ErrorMessage = "El código interno no puede exceder 50 caracteres")]
    public string CodigoInterno { get; set; } = string.Empty;

    [Required(ErrorMessage = "El activo fijo es requerido")]
    [MaxLength(50, ErrorMessage = "El activo fijo no puede exceder 50 caracteres")]
    public string ActivoFijo { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "El serial del fabricante no puede exceder 100 caracteres")]
    public string? SerialFabricante { get; set; }

    [Required(ErrorMessage = "El nombre del equipo es requerido")]
    [MaxLength(200, ErrorMessage = "El nombre del equipo no puede exceder 200 caracteres")]
    public string NombreEquipo { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "La marca no puede exceder 100 caracteres")]
    public string? Marca { get; set; }

    [MaxLength(100, ErrorMessage = "El modelo no puede exceder 100 caracteres")]
    public string? Modelo { get; set; }

    [MaxLength(200, ErrorMessage = "El fabricante no puede exceder 200 caracteres")]
    public string? Fabricante { get; set; }

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    public long? IdCategoria { get; set; }

    public long? IdUbicacion { get; set; }

    public string? QRCode { get; set; }
}
