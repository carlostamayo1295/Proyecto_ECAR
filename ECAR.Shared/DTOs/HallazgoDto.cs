using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class HallazgoDto
{
    public long IdHallazgo { get; set; }
    public long IdInspeccion { get; set; }
    public long? IdPregunta { get; set; }
    public string? NombreEquipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Criticidad { get; set; }
    public string? Estado { get; set; }
    public string? ResponsableId { get; set; }
    public DateTime? FechaCompromiso { get; set; }
    public string? AccionCorrectiva { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public class CreateHallazgoDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    public long? IdPregunta { get; set; }

    [Required(ErrorMessage = "La descripción es requerida")]
    [MaxLength(500)]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    [MaxLength(100)]
    public string? ResponsableId { get; set; }

    public DateTime? FechaCompromiso { get; set; }
}

public class UpdateHallazgoDto
{
    public string? Descripcion { get; set; }

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    [MaxLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
    public string? Estado { get; set; }

    [MaxLength(100)]
    public string? ResponsableId { get; set; }

    public DateTime? FechaCompromiso { get; set; }
}

public class CerrarHallazgoDto
{
    [Required(ErrorMessage = "La acción correctiva es obligatoria para cerrar el hallazgo.")]
    public string AccionCorrectiva { get; set; } = string.Empty;
}

public class AnularHallazgoDto
{
    [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
    [MaxLength(500)]
    public string MotivoAnulacion { get; set; } = string.Empty;
}