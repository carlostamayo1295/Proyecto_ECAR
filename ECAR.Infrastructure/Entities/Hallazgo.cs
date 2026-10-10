using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ECAR.Shared;

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

    [Required]
    [Column("Descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("Criticidad")]
    public string? Criticidad { get; set; }

    /// <summary>Uno de HallazgoEstados (ciclo de vida de PLAN_FASE4_TAREAS §3.5).</summary>
    [Required]
    [MaxLength(20)]
    [Column("Estado")]
    public string Estado { get; set; } = HallazgoEstados.Abierto;

    [Required]
    [Column("FechaRegistro")]
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    // Fase 4 (§3.1): origen, responsable y ciclo de vida. Un hallazgo nunca se borra, se anula.

    /// <summary>Uno de HallazgoOrigenes: Novedad (creado al firmar) o Manual.</summary>
    [Required]
    [MaxLength(20)]
    [Column("Origen")]
    public string Origen { get; set; } = HallazgoOrigenes.Manual;

    /// <summary>La pregunta respondida "No" que lo originó; null en los manuales.</summary>
    [Column("IdPregunta")]
    public long? IdPregunta { get; set; }

    [Required]
    [Column("IdUsuarioRegistro")]
    public long IdUsuarioRegistro { get; set; }

    [Column("IdUsuarioResponsable")]
    public long? IdUsuarioResponsable { get; set; }

    [Column("FechaCompromiso")]
    public DateTime? FechaCompromiso { get; set; }

    /// <summary>Obligatoria para pasar a Cerrado.</summary>
    [Column("AccionCorrectiva")]
    public string? AccionCorrectiva { get; set; }

    [Column("FechaCierre")]
    public DateTime? FechaCierre { get; set; }

    [Column("IdUsuarioCierre")]
    public long? IdUsuarioCierre { get; set; }

    [MaxLength(500)]
    [Column("MotivoAnulacion")]
    public string? MotivoAnulacion { get; set; }

    // Propiedades de navegación
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;

    [ForeignKey("IdPregunta")]
    public virtual PreguntaChecklist? Pregunta { get; set; }

    [ForeignKey("IdUsuarioRegistro")]
    public virtual Usuario UsuarioRegistro { get; set; } = null!;

    [ForeignKey("IdUsuarioResponsable")]
    public virtual Usuario? UsuarioResponsable { get; set; }

    [ForeignKey("IdUsuarioCierre")]
    public virtual Usuario? UsuarioCierre { get; set; }
}