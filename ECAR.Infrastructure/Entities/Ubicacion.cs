using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Ubicaciones")]
public class Ubicacion
{
    [Key]
    [Column("IdUbicacion")]
    public long IdUbicacion { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Planta")]
    public string Planta { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("Area")]
    public string Area { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("Descripcion")]
    public string? Descripcion { get; set; }

    // Navigation properties
    public virtual ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
}