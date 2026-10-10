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

    // Fase 4: seguridad de la cuenta (PLAN_FASE4_TAREAS §3.1 y §3.8, Parte 11 §11.300).

    [Column("IntentosFallidos")]
    public int IntentosFallidos { get; set; }

    [Column("BloqueadoHasta")]
    public DateTime? BloqueadoHasta { get; set; }

    [Column("FechaCambioPassword")]
    public DateTime? FechaCambioPassword { get; set; }

    [Column("DebeCambiarPassword")]
    public bool DebeCambiarPassword { get; set; }

    /// <summary>
    /// Va en cada token. ECARDbContext lo renueva al desactivar al usuario o cambiar su
    /// contraseña, y desde ese momento sus tokens anteriores dejan de valer (§11.300(c)).
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("SecurityStamp")]
    public string SecurityStamp { get; set; } = NuevoSecurityStamp();

    [Column("UltimoAcceso")]
    public DateTime? UltimoAcceso { get; set; }

    public static string NuevoSecurityStamp() => Guid.NewGuid().ToString("N");

    // Propiedades de navegación
    public virtual ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public virtual ICollection<Inspeccion> Inspecciones { get; set; } = new List<Inspeccion>();
    public virtual ICollection<HistorialPassword> HistorialPasswords { get; set; } = new List<HistorialPassword>();
}