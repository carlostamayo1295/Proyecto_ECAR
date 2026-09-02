using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Hallazgos")]
public class Hallazgo
{
    [Key]
    [Column("IdHallazgo")]
    public long IdHallazgo { get; set; }

    [Required]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    [Required]
    [Column("Descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("Criticidad")]
    public string? Criticidad { get; set; }

    [MaxLength(20)]
    [Column("Estado")]
    public string? Estado { get; set; }

    [Required]
    [Column("FechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Propiedades de navegación
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;
}