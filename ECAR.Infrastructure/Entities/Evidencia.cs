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
    [MaxLength(200)]
    [Column("NombreOriginal")]
    public string NombreOriginal { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("TipoContenido")]
    public string TipoContenido { get; set; } = string.Empty;

    [Column("TamanoBytes")]
    public long TamanoBytes { get; set; }

    [Required]
    [Column("FechaCarga")]
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("IdUsuarioCarga")]
    public long IdUsuarioCarga { get; set; }

    // Propiedades de navegación
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;

    [ForeignKey("IdUsuarioCarga")]
    public virtual Usuario UsuarioCargaDetalle { get; set; } = null!;
}
