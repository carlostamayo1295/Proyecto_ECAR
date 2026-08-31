using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class InspeccionDto
{
    public long IdInspeccion { get; set; }
    public long IdEquipo { get; set; }
    public string? NombreEquipo { get; set; }
    public long IdUsuario { get; set; }
    public string? NombreUsuario { get; set; }
    public DateTime FechaInspeccion { get; set; }
    public string? Resultado { get; set; }
    public string? Observaciones { get; set; }
    public bool TieneFirma { get; set; }
    public int TotalEvidencias { get; set; }
    public int TotalHallazgos { get; set; }
}

public class CreateInspeccionDto
{
    [Required(ErrorMessage = "El equipo es requerido")]
    public long IdEquipo { get; set; }

    [Required(ErrorMessage = "El inspector es requerido")]
    public long IdUsuario { get; set; }

    [Required(ErrorMessage = "La fecha de inspección es requerida")]
    public DateTime FechaInspeccion { get; set; }

    [MaxLength(50, ErrorMessage = "El resultado no puede exceder 50 caracteres")]
    public string? Resultado { get; set; }

    public string? Observaciones { get; set; }

    public string? FirmaDigital { get; set; }
}

public class UpdateInspeccionDto
{
    [MaxLength(50, ErrorMessage = "El resultado no puede exceder 50 caracteres")]
    public string? Resultado { get; set; }

    public string? Observaciones { get; set; }

    public string? FirmaDigital { get; set; }
}
