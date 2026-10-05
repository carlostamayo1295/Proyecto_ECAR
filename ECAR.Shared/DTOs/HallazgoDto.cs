using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Fila del listado de hallazgos. Las propiedades a partir de Origen son de la Fase 4
/// (PLAN_FASE4_TAREAS §3.1 y §3.5); mientras BE-1 no publique sus endpoints llegan vacías.
/// </summary>
public class HallazgoDto
{
    public long IdHallazgo { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string? Criticidad { get; set; }

    /// <summary>Uno de <see cref="HallazgoEstados"/>.</summary>
    public string? Estado { get; set; }

    public DateTime FechaRegistro { get; set; }

    /// <summary><see cref="HallazgoOrigenes.Novedad"/> o <see cref="HallazgoOrigenes.Manual"/>.</summary>
    public string? Origen { get; set; }

    public long IdEquipo { get; set; }
    public string? CodigoInterno { get; set; }

    /// <summary>"Planta - Área" del equipo: columna "Área" del reporte de novedades (SRS §7).</summary>
    public string? Area { get; set; }

    public long? IdUsuarioRegistro { get; set; }
    public string? NombreUsuarioRegistro { get; set; }

    public long? IdUsuarioResponsable { get; set; }

    /// <summary>Columna "Responsable" del reporte de novedades (SRS §7).</summary>
    public string? NombreResponsable { get; set; }

    public DateTime? FechaCompromiso { get; set; }
    public string? AccionCorrectiva { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? NombreUsuarioCierre { get; set; }
    public string? MotivoAnulacion { get; set; }
}

/// <summary>
/// GET /api/hallazgos/{id} (endpoint 5): el hallazgo y de dónde salió.
/// </summary>
public class HallazgoDetalleDto : HallazgoDto
{
    /// <summary>Pregunta del checklist que originó la novedad; null en los manuales.</summary>
    public long? IdPregunta { get; set; }
    public string? Pregunta { get; set; }

    /// <summary>Observación que el técnico escribió en esa respuesta.</summary>
    public string? ObservacionRespuesta { get; set; }

    public DateTime FechaInspeccion { get; set; }
    public string? EstadoInspeccion { get; set; }
    public string? NombreInspector { get; set; }
}

/// <summary>Filtros de GET /api/hallazgos (endpoint 4). Todos opcionales.</summary>
public class HallazgoFiltroDto
{
    public string? Search { get; set; }
    public string? Estado { get; set; }
    public string? Criticidad { get; set; }
    public long? IdEquipo { get; set; }
    public long? IdInspeccion { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class CreateHallazgoDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "La descripción es requerida")]
    public string Descripcion { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    // Fase 4
    public long? IdUsuarioResponsable { get; set; }
    public DateTime? FechaCompromiso { get; set; }
}

/// <summary>
/// PUT /api/hallazgos/{id} (endpoint 7). En la Fase 4 el estado se cambia con
/// POST {id}/estado y la descripción de un hallazgo de origen "Novedad" no se edita:
/// BE-1 retira Estado y Descripcion de aquí cuando publique esos endpoints.
/// </summary>
public class UpdateHallazgoDto
{
    public string? Descripcion { get; set; }

    [MaxLength(20, ErrorMessage = "La criticidad no puede exceder 20 caracteres")]
    public string? Criticidad { get; set; }

    [MaxLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
    public string? Estado { get; set; }

    // Fase 4
    public long? IdUsuarioResponsable { get; set; }
    public DateTime? FechaCompromiso { get; set; }

    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string? Motivo { get; set; }
}

/// <summary>POST /api/hallazgos/{id}/estado (endpoint 8).</summary>
public class CambiarEstadoHallazgoDto
{
    [Required(ErrorMessage = "El estado es requerido")]
    public string Estado { get; set; } = string.Empty;

    /// <summary>Obligatoria para pasar a <see cref="HallazgoEstados.Cerrado"/>.</summary>
    public string? AccionCorrectiva { get; set; }

    /// <summary>Obligatorio para reabrir un hallazgo cerrado.</summary>
    [MaxLength(500, ErrorMessage = "El motivo no puede exceder 500 caracteres")]
    public string? Motivo { get; set; }
}
