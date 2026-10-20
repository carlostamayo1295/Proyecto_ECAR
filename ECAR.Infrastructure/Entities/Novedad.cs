using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ECAR.Shared;

namespace ECAR.Infrastructure.Entities;

/// <summary>
/// Una respuesta "No" de una inspección firmada (regla 4 del SRS). La crea el servidor al firmar,
/// una por pregunta, y se resuelve con una firma electrónica: lo que se hizo, la firma manuscrita,
/// la contraseña de quien resuelve y un sello HMAC, igual que la inspección. Nunca se borra.
/// Reemplaza a la tabla Hallazgos de la Fase 4.
/// </summary>
[Table("Novedades")]
public class Novedad
{
    [Key]
    [Column("IdNovedad")]
    public long IdNovedad { get; set; }

    [Required]
    [Column("IdInspeccion")]
    public long IdInspeccion { get; set; }

    /// <summary>La pregunta respondida "No".</summary>
    [Required]
    [Column("IdPregunta")]
    public long IdPregunta { get; set; }

    /// <summary>La respuesta "No" concreta, firmada con la inspección.</summary>
    [Required]
    [Column("IdRespuesta")]
    public long IdRespuesta { get; set; }

    /// <summary>"Pregunta: observación", tal como quedó en la inspección firmada.</summary>
    [Required]
    [Column("Descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>La del equipo al firmar la inspección.</summary>
    [MaxLength(20)]
    [Column("Criticidad")]
    public string? Criticidad { get; set; }

    /// <summary>Uno de <see cref="NovedadEstados"/>.</summary>
    [Required]
    [MaxLength(20)]
    [Column("Estado")]
    public string Estado { get; set; } = NovedadEstados.Pendiente;

    [Required]
    [Column("FechaRegistro")]
    public DateTime FechaRegistro { get; set; }

    /// <summary>El inspector que firmó la inspección.</summary>
    [Required]
    [Column("IdUsuarioRegistro")]
    public long IdUsuarioRegistro { get; set; }

    // Resolución firmada (Parte 11 §11.50 y §11.70). Puede firmarla un Técnico o un
    // Administrador distinto del inspector.

    /// <summary>Lo que se hizo para resolver la novedad.</summary>
    [MaxLength(2000)]
    [Column("Solucion")]
    public string? Solucion { get; set; }

    [Column("FechaResolucion")]
    public DateTime? FechaResolucion { get; set; }

    [Column("IdUsuarioResolucion")]
    public long? IdUsuarioResolucion { get; set; }

    /// <summary>PNG de la firma manuscrita en base64. En la auditoría se guarda su SHA-256.</summary>
    [Column("FirmaDigital")]
    public string? FirmaDigital { get; set; }

    [MaxLength(200)]
    [Column("FirmaNombre")]
    public string? FirmaNombre { get; set; }

    [MaxLength(200)]
    [Column("FirmaSignificado")]
    public string? FirmaSignificado { get; set; }

    [MaxLength(30)]
    [Column("FirmaAlgoritmo")]
    public string? FirmaAlgoritmo { get; set; }

    [MaxLength(64)]
    [Column("FirmaHash")]
    public string? FirmaHash { get; set; }

    // Anulación: solo el Administrador, con motivo, de una novedad registrada por error.

    [MaxLength(500)]
    [Column("MotivoAnulacion")]
    public string? MotivoAnulacion { get; set; }

    [Column("FechaAnulacion")]
    public DateTime? FechaAnulacion { get; set; }

    [Column("IdUsuarioAnulacion")]
    public long? IdUsuarioAnulacion { get; set; }

    /// <summary>Cuándo salió el correo a la mesa de ayuda; null si todavía no se ha podido enviar.</summary>
    [Column("FechaNotificacion")]
    public DateTime? FechaNotificacion { get; set; }

    /// <summary>
    /// Control de concurrencia: si dos personas resuelven la misma novedad a la vez, la segunda
    /// falla en lugar de pisar a la primera. El arreglo vacío solo existe para el proveedor en
    /// memoria de las pruebas: EF no envía este valor en el INSERT (lo genera SQL Server).
    /// </summary>
    [Timestamp]
    [Column("RowVersion")]
    public byte[] RowVersion { get; set; } = [];

    // Propiedades de navegación
    [ForeignKey("IdInspeccion")]
    public virtual Inspeccion Inspeccion { get; set; } = null!;

    [ForeignKey("IdPregunta")]
    public virtual PreguntaChecklist Pregunta { get; set; } = null!;

    [ForeignKey("IdRespuesta")]
    public virtual RespuestaInspeccion Respuesta { get; set; } = null!;

    [ForeignKey("IdUsuarioRegistro")]
    public virtual Usuario UsuarioRegistro { get; set; } = null!;

    [ForeignKey("IdUsuarioResolucion")]
    public virtual Usuario? UsuarioResolucion { get; set; }

    [ForeignKey("IdUsuarioAnulacion")]
    public virtual Usuario? UsuarioAnulacion { get; set; }
}
