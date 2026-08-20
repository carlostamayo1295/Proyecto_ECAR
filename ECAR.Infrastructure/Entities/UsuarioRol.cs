using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("UsuarioRol")]
public class UsuarioRol
{
    [Key]
    [Column("Id")]
    public long Id { get; set; }

    [Required]
    [Column("IdUsuario")]
    public long IdUsuario { get; set; }

    [Required]
    [Column("IdRol")]
    public long IdRol { get; set; }

    // Navigation properties
    [ForeignKey("IdUsuario")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("IdRol")]
    public virtual Rol Rol { get; set; } = null!;
}