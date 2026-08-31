using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Equipos")]
public class Equipo
{
    [Key]
    [Column("IdEquipo")]
    public long IdEquipo { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("CodigoInterno")]
    public string CodigoInterno { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("ActivoFijo")]
    public string ActivoFijo { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("SerialFabricante")]
    public string? SerialFabricante { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("NombreEquipo")]
    public string NombreEquipo { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("Marca")]
    public string? Marca { get; set; }

    [MaxLength(100)]
    [Column("Modelo")]
    public string? Modelo { get; set; }

    [MaxLength(200)]
    [Column("Fabricante")]
    public string? Fabricante { get; set; }

    [MaxLength(20)]
    [Column("Criticidad")]
    public string? Criticidad { get; set; }

    [Column("IdCategoria")]
    public long? IdCategoria { get; set; }

    [Column("IdUbicacion")]
    public long? IdUbicacion { get; set; }

    [Column("QRCode")]
    public string? QRCode { get; set; }

    [Column("Activo")]
    public bool Activo { get; set; } = true;

    [Column("FechaCreacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("IdCategoria")]
    public virtual CategoriaEquipo? Categoria { get; set; }

    [ForeignKey("IdUbicacion")]
    public virtual Ubicacion? Ubicacion { get; set; }

    public virtual ICollection<Inspeccion> Inspecciones { get; set; } = new List<Inspeccion>();
}