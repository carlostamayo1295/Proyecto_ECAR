using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ECAR.Shared;

namespace ECAR.Infrastructure.Entities;

[Table("Inspecciones")]
public class Inspeccion
{
    [Key]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    [Required]
    [Column("IdEquipo")]
    public long IdEquipo { get; set; }

    [Required]
    [Column("IdUsuario")]
    public long IdUsuario { get; set; }

    [Required]
    [Column("IdChecklist")]
    public long IdChecklist { get; set; }

    [Required]
    [Column("FechaInspeccion")]
    public DateTime FechaInspeccion { get; set; }

    [MaxLength(50)]
    [Column("Resultado")]
    public string? Resultado { get; set; }

    [Column("Observaciones")]
    public string? Observaciones { get; set; }

    [Column("FirmaDigital")]
    public string? FirmaDigital { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("Estado")]
    public string Estado { get; set; } = InspeccionEstados.EnCurso;

    [Column("FechaCierre")]
    public DateTime? FechaCierre { get; set; }

    [MaxLength(64)]
    [Column("FirmaHash")]
    public string? FirmaHash { get; set; }

    // Fase 4: manifestación de la firma (Parte 11 §11.50), guardada como estaba al firmar.

    [MaxLength(200)]
    [Column("FirmaNombre")]
    public string? FirmaNombre { get; set; }

    [MaxLength(200)]
    [Column("FirmaSignificado")]
    public string? FirmaSignificado { get; set; }

    /// <summary>Cómo se calculó FirmaHash: null o "SHA256" en las cerradas antes de la Fase 4, "HMACSHA256-v1" después.</summary>
    [MaxLength(30)]
    [Column("FirmaAlgoritmo")]
    public string? FirmaAlgoritmo { get; set; }

    // Fase 4: una inspección en curso ya no se borra, se anula con motivo (§3.6).

    [MaxLength(500)]
    [Column("MotivoAnulacion")]
    public string? MotivoAnulacion { get; set; }

    [Column("FechaAnulacion")]
    public DateTime? FechaAnulacion { get; set; }

    [Column("IdUsuarioAnulacion")]
    public long? IdUsuarioAnulacion { get; set; }

    // Propiedades de navegación
    [ForeignKey("IdEquipo")]
    public virtual Equipo Equipo { get; set; } = null!;

    [ForeignKey("IdUsuario")]
    public virtual Usuario Usuario { get; set; } = null!;

    [ForeignKey("IdChecklist")]
    public virtual Checklist Checklist { get; set; } = null!;

    [ForeignKey("IdUsuarioAnulacion")]
    public virtual Usuario? UsuarioAnulacion { get; set; }

    public virtual ICollection<RespuestaInspeccion> Respuestas { get; set; } = new List<RespuestaInspeccion>();
    public virtual ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();
    public virtual ICollection<Hallazgo> Hallazgos { get; set; } = new List<Hallazgo>();
}
