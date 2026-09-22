namespace ECAR.Shared.DTOs;

/// <summary>
/// Estado completo de una inspección en ejecución. Lo devuelven POST /api/inspecciones/iniciar
/// y GET /api/inspecciones/{id}/ejecucion.
/// Trae cabecera, preguntas y evidencias en una sola respuesta: la pantalla de ejecución corre
/// en un teléfono y no debe encadenar varias llamadas para dibujarse.
/// </summary>
public class InspeccionEjecucionDto
{
    public long IdInspeccion { get; set; }

    /// <summary>Valor de <see cref="InspeccionEstados"/>. Si llega Cerrada, la pantalla redirige al resultado.</summary>
    public string Estado { get; set; } = InspeccionEstados.EnCurso;

    public DateTime FechaInspeccion { get; set; }

    // --- Cabecera (solo lectura en pantalla) ---
    public long IdEquipo { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public string NombreEquipo { get; set; } = string.Empty;
    public string? UbicacionNombre { get; set; }
    public string? Criticidad { get; set; }

    public long IdChecklist { get; set; }
    public string NombreChecklist { get; set; } = string.Empty;

    /// <summary>Versión del checklist con la que se ejecuta: debe verse en pantalla (trazabilidad).</summary>
    public string VersionChecklist { get; set; } = string.Empty;

    public long IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;

    // --- Contenido del flujo ---
    public List<PreguntaEjecucionDto> Preguntas { get; set; } = new();
    public List<EvidenciaDto> Evidencias { get; set; } = new();

    // --- Contadores calculados por el servidor ---
    // El cliente los recalcula localmente tras cada guardado solo para refrescar la UI.
    public int TotalObligatorias { get; set; }
    public int ObligatoriasRespondidas { get; set; }
    public int TotalNovedades { get; set; }

    public bool EstaCerrada => Estado == InspeccionEstados.Cerrada;

    /// <summary>true cuando no quedan obligatorias sin responder y se puede pasar a la firma.</summary>
    public bool PuedeFirmar => ObligatoriasRespondidas >= TotalObligatorias;
}
