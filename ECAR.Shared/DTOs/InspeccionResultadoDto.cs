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

    /// <summary>Siempre <see cref="InspeccionEstados.Cerrada"/> en este DTO.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Valor de <see cref="InspeccionResultados"/>: lo calcula el servidor al cerrar.</summary>
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
    public int TotalNovedades { get; set; }

    // --- Firma ---
    public bool TieneFirma { get; set; }

    /// <summary>PNG de la firma en base64, para mostrarla en el resultado y al imprimir.</summary>
    public string? FirmaPngBase64 { get; set; }

    /// <summary>SHA-256 del contenido firmado. Visible para el Auditor (regla 6 del SRS).</summary>
    public string? FirmaHash { get; set; }

    public bool ConNovedad => Resultado == InspeccionResultados.ConNovedad;
}
