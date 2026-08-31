using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class UpdateUbicacionDto
{
    [Required(ErrorMessage = "La planta es requerida")]
    [MaxLength(100, ErrorMessage = "La planta no puede exceder 100 caracteres")]
    public string Planta { get; set; } = string.Empty;

    [Required(ErrorMessage = "El área es requerida")]
    [MaxLength(100, ErrorMessage = "El área no puede exceder 100 caracteres")]
    public string Area { get; set; } = string.Empty;

    [MaxLength(300, ErrorMessage = "La descripción no puede exceder 300 caracteres")]
    public string? Descripcion { get; set; }
}
