using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Inspecciones")]
public class Inspeccion
{
    [Key]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    [Required]
    [Column("IdEquipo")]
    public long IdEquipo { get; set; }

    [Required]
    [Column("IdUsuario")]
    public long IdUsuario { get; set; }

    [Required]
    [Column("FechaInspeccion")]
    public DateTime FechaInspeccion { get; set; }

    [MaxLength(50)]
    [Column("Resultado")]
    public string? Resultado { get; set; }

    [Column("Observaciones")]
    public string? Observaciones { get; set; }

    [Column("FirmaDigital")]
    public string? FirmaDigital { get; set; }

    // Propiedades de navegación
    [ForeignKey("IdEquipo")]
    public virtual Equipo Equipo { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    public virtual Usuario Usuario { get; set; } = null!;

    public virtual ICollection<RespuestaInspeccion> Respuestas { get; set; } = new List<RespuestaInspeccion>();
    public virtual ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
    public virtual ICollection<Hallazgo> Hallazgos { get; set; } = new List<Hallazgo>();
}