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

    /// <summary>
    /// Uno de InspeccionResultados: lo calcula el servidor al firmar. "Pendiente" mientras queden
    /// novedades sin resolver; por eso no entra en el sello HMACSHA256-v2.
    /// </summary>
    [MaxLength(50)]
    [Column("Resultado")]
    public string? Resultado { get; set; }

    [Column("Observaciones")]
    public string? Observaciones { get; set; }

    /// <summary>Sede de ECAR (Sedes: Rionegro o Medellín), obligatoria para firmar desde el 29/10/2026.</summary>
    [MaxLength(30)]
    [Column("Sede")]
    public string? Sede { get; set; }

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

    /// <summary>
    /// Cómo se calculó FirmaHash: null o "SHA256" en las cerradas antes de la Fase 4,
    /// "HMACSHA256-v1" en la Fase 4 y "HMACSHA256-v2" (sin el resultado) desde las novedades.
    /// </summary>
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

    /// <summary>
    /// Control de concurrencia: SQL Server lo cambia en cada UPDATE de la fila. Si se firma y se
    /// anula la misma inspección a la vez, la segunda petición falla en lugar de pisar a la
    /// primera. El arreglo vacío solo existe para el proveedor en memoria de las pruebas: EF no
    /// envía este valor en el INSERT (lo genera SQL Server).
    /// </summary>
    [Timestamp]
    [Column("RowVersion")]
    public byte[] RowVersion { get; set; } = [];

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
    public virtual ICollection<Novedad> Novedades { get; set; } = new List<Novedad>();
}
