using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class HallazgoDto
{
    public long IdHallazgo { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Criticidad { get; set; }
    public string? Estado { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public class CreateHallazgoDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "La descripción es requerida")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }
}

public class UpdateHallazgoDto
{
    public string? Descripcion { get; set; }

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    [MaxLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
    public string? Estado { get; set; }
}
