using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("CategoriasEquipo")]
public class CategoriaEquipo
{
    [Key]
    [Column("IdCategoria")]
    public long IdCategoria { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("Descripcion")]
    public string? Descripcion { get; set; }

    // Navigation properties
    public virtual ICollection<Equipo> Equipos { get; set; } = new List<Equipo>();
}