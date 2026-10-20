namespace ECAR.Shared.DTOs;

/// <summary>
/// Inspección cerrada, en modo lectura. La devuelven POST /api/inspecciones/{id}/firmar y
/// GET /api/inspecciones/{id}/resultado.
/// Reutiliza <see cref="PreguntaEjecucionDto"/> y <see cref="EvidenciaDto"/> para que la
/// pantalla de resultado pueda reaprovechar los componentes de la ejecución.
/// </summary>
public class InspeccionResultadoDto
{
    public long IdInspeccion { get; set; }

    /// <summary>
    /// <see cref="InspeccionEstados.Cerrada"/> o <see cref="InspeccionEstados.NovedadesPendientes"/>
    /// en este DTO: las dos están firmadas.
    /// </summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>
    /// Valor de <see cref="InspeccionResultados"/>: lo calcula el servidor. "Pendiente" mientras
    /// queden novedades sin resolver.
    /// </summary>
    public string Resultado { get; set; } = string.Empty;

    public DateTime FechaInspeccion { get; set; }
    public DateTime? FechaCierre { get; set; }
    public string? Observaciones { get; set; }

    // --- Cabecera ---
    public long IdEquipo { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string NombreEquipo { get; set; } = string.Empty;
    public string? UbicacionNombre { get; set; }
    public string NombreChecklist { get; set; } = string.Empty;
    public string VersionChecklist { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;

    // --- Detalle ---
    public List<PreguntaEjecucionDto> Preguntas { get; set; } = new();
    public List<EvidenciaDto> Evidencias { get; set; } = new();

    /// <summary>Respuestas "No": cada una es una novedad.</summary>
    public int TotalNovedades { get; set; }

    /// <summary>Novedades que siguen sin resolver.</summary>
    public int NovedadesPendientes { get; set; }

    // --- Firma ---
    public bool TieneFirma { get; set; }

    /// <summary>PNG de la firma en base64, para mostrarla en el resultado y al imprimir.</summary>
    public string? FirmaPngBase64 { get; set; }

    /// <summary>SHA-256 del contenido firmado. Visible para el Auditor (regla 6 del SRS).</summary>
    public string? FirmaHash { get; set; }

    // --- Manifestación de la firma (Fase 4, Parte 11 §11.50) ---

    /// <summary>Nombre del firmante tal como era al firmar.</summary>
    public string? FirmaNombre { get; set; }

    /// <summary>Significado de la firma, p. ej. "Responsable de la ejecución de la inspección".</summary>
    public string? FirmaSignificado { get; set; }

    /// <summary>Cómo se calculó FirmaHash: "SHA256-v0" (antes de la Fase 4), "HMACSHA256-v1" o "HMACSHA256-v2".</summary>
    public string? FirmaAlgoritmo { get; set; }

    /// <summary>Reportó al menos una novedad, resuelta o no.</summary>
    public bool ConNovedad => TotalNovedades > 0;
}
