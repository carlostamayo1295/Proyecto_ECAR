using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Roles")]
public class Rol
{
    [Key]
    [Column("IdRol")]
    public long IdRol { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("Nombre")]
    public string Nombre { get; set; } = string.Empty;

    // Propiedades de navegación
    public virtual ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
}