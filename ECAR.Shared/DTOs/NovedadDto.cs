using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Fila del listado de novedades (GET /api/novedades). Una novedad es una respuesta "No" de una
/// inspección firmada; nace sola al firmar y se resuelve con firma.
/// </summary>
public class NovedadDto
{
    public long IdNovedad { get; set; }
    public long IdInspeccion { get; set; }

    public long IdEquipo { get; set; }
    public string? NombreEquipo { get; set; }
    public string? CodigoInterno { get; set; }

    /// <summary>"Planta - Área" del equipo: columna "Área" del reporte de novedades (SRS §7).</summary>
    public string? Area { get; set; }

    /// <summary>La pregunta del checklist respondida "No".</summary>
    public long IdPregunta { get; set; }
    public string? Pregunta { get; set; }

    /// <summary>"Pregunta: observación", tal como quedó en la inspección firmada. No se edita.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>La del equipo al momento de firmar la inspección.</summary>
    public string? Criticidad { get; set; }

    /// <summary>Uno de <see cref="NovedadEstados"/>.</summary>
    public string Estado { get; set; } = NovedadEstados.Pendiente;

    public DateTime FechaRegistro { get; set; }

    /// <summary>El inspector que la reportó al firmar la inspección.</summary>
    public long IdUsuarioRegistro { get; set; }
    public string? NombreInspector { get; set; }

    public DateTime? FechaResolucion { get; set; }
    public string? NombreUsuarioResolucion { get; set; }

    /// <summary>Cuándo salió el correo a la mesa de ayuda; null si todavía no se ha podido enviar.</summary>
    public DateTime? FechaNotificacion { get; set; }

    public string? MotivoAnulacion { get; set; }
}

/// <summary>GET /api/novedades/{id}: la novedad, la inspección de la que salió y su resolución firmada.</summary>
public class NovedadDetalleDto : NovedadDto
{
    /// <summary>Observación que el inspector escribió en la respuesta "No".</summary>
    public string? ObservacionRespuesta { get; set; }

    public DateTime FechaInspeccion { get; set; }
    public string? NombreChecklist { get; set; }

    /// <summary>Uno de <see cref="InspeccionEstados"/>.</summary>
    public string? EstadoInspeccion { get; set; }

    // --- Resolución (Parte 11 §11.50): tal como quedó al firmarla ---

    /// <summary>Lo que se hizo para resolver la novedad.</summary>
    public string? Solucion { get; set; }

    /// <summary>PNG de la firma manuscrita en base64.</summary>
    public string? FirmaPngBase64 { get; set; }

    public string? FirmaNombre { get; set; }

    /// <summary>"Responsable de la resolución de la novedad".</summary>
    public string? FirmaSignificado { get; set; }

    public string? FirmaAlgoritmo { get; set; }

    /// <summary>Sello HMAC de la resolución. La pantalla lo muestra al Administrador y al Auditor.</summary>
    public string? FirmaHash { get; set; }

    // --- Anulación ---
    public DateTime? FechaAnulacion { get; set; }
    public string? NombreUsuarioAnulacion { get; set; }
}

/// <summary>Filtros de GET /api/novedades. Todos opcionales y combinables.</summary>
public class NovedadFiltroDto
{
    public string? Search { get; set; }
    public string? Estado { get; set; }
    public string? Criticidad { get; set; }
    public long? IdEquipo { get; set; }
    public long? IdInspeccion { get; set; }

    /// <summary>Día de registro desde el que se busca, en hora de Colombia.</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Día de registro hasta el que se busca (incluido), en hora de Colombia.</summary>
    public DateTime? Hasta { get; set; }
}

/// <summary>
/// POST /api/novedades/{id}/resolver. Resolver es firmar: la firma manuscrita y la contraseña
/// (Parte 11 §11.200(a)(1)) van con el texto de lo que se hizo. Una contraseña errónea responde
/// 400 sin Errors y una cuenta bloqueada, 423; el resto de fallos de contenido, 400 con Errors.
/// </summary>
public class ResolverNovedadDto
{
    public const int LongitudMinimaSolucion = 10;
    public const int LongitudMaximaSolucion = 2000;

    [Required(ErrorMessage = "Describa qué se hizo para resolver la novedad")]
    [StringLength(LongitudMaximaSolucion, MinimumLength = LongitudMinimaSolucion,
        ErrorMessage = "Lo que se hizo debe tener entre 10 y 2000 caracteres")]
    public string Solucion { get; set; } = string.Empty;

    /// <summary>PNG de la firma en base64, <b>sin</b> el prefijo "data:image/png;base64,".</summary>
    [Required(ErrorMessage = "La firma es requerida")]
    [MaxLength(280_000, ErrorMessage = "La firma no puede exceder 200 KB")]
    public string FirmaPngBase64 { get; set; } = string.Empty;

    public string? Password { get; set; }
}
