using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Usuarios")]
public class Usuario
{
    [Key]
    [Column("IdUsuario")]
    public long IdUsuario { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("Correo")]
    public string Correo { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("UsuarioAD")]
    public string? UsuarioAD { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("Activo")]
    public bool Activo { get; set; } = true;

    // Navigation properties
    public virtual ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public virtual ICollection<Inspeccion> Inspecciones { get; set; } = new List<Inspeccion>();
}