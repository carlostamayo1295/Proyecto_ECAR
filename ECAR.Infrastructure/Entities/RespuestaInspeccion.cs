using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("RespuestasInspeccion")]
public class RespuestaInspeccion
{
    [Key]
    [Column("IdRespuesta")]
    public long IdRespuesta { get; set; }

    [Required]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    [Required]
    [Column("IdPregunta")]
    public long IdPregunta { get; set; }

    [Column("Respuesta")]
    public string? Respuesta { get; set; }

    [Column("Observacion")]
    public string? Observacion { get; set; }

    // Navigation properties
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;

    [ForeignKey("IdPregunta")]
    public virtual PreguntaChecklist Pregunta { get; set; } = null!;
}