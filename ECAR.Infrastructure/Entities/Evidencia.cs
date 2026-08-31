using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Evidencias")]
public class Evidencia
{
    [Key]
    [Column("IdEvidencia")]
    public long IdEvidencia { get; set; }

    [Required]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    [Required]
    [Column("Archivo")]
    public string Archivo { get; set; } = string.Empty;

    [Required]
    [Column("FechaCarga")]
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(100)]
    [Column("UsuarioCarga")]
    public string UsuarioCarga { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;
}