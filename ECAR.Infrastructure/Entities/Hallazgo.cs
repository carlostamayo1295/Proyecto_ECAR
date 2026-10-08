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

    // Nuevo: Vínculo opcional con la pregunta si se genera por respuesta negativa
    [Column("IdPregunta")]
    public long? IdPregunta { get; set; }

    [Required]
    [Column("Descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("Criticidad")]
    public string? Criticidad { get; set; }

    [MaxLength(20)]
    [Column("Estado")]
    public string? Estado { get; set; } = "Abierto"; // Estado por defecto

    // Nuevos campos requeridos por ECAR-211
    [MaxLength(100)]
    [Column("ResponsableId")]
    public string? ResponsableId { get; set; }

    [Column("FechaCompromiso")]
    public DateTime? FechaCompromiso { get; set; }

    [Column("AccionCorrectiva")]
    public string? AccionCorrectiva { get; set; }

    [Column("MotivoAnulacion")]
    public string? MotivoAnulacion { get; set; }

    [Required]
    [Column("FechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("IsDeleted")]
    public bool IsDeleted { get; set; } = false; // Requisito: Borrado lógico

    // Propiedades de navegación
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;
}