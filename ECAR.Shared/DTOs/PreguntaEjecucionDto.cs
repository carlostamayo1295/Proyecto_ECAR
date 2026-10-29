namespace ECAR.Shared.DTOs;

/// <summary>
/// Una pregunta del checklist junto con la respuesta ya registrada en la inspección.
/// Une <see cref="PreguntaChecklistDto"/> y <see cref="RespuestaInspeccionDto"/> para que la
/// pantalla de ejecución pinte la lista en un solo recorrido, sin cruzar dos colecciones.
/// </summary>
public class PreguntaEjecucionDto
{
    public long IdPregunta { get; set; }
    public int Orden { get; set; }
    public string Pregunta { get; set; } = string.Empty;

    /// <summary>Valor del catálogo <see cref="TiposRespuesta"/>: SiNo o Texto.</summary>
    public string TipoRespuesta { get; set; } = string.Empty;

    public bool Obligatoria { get; set; }

    /// <summary>Id de la fila de respuesta, si ya existe. El upsert va por IdPregunta, no por este id.</summary>
    public long? IdRespuesta { get; set; }

    /// <summary>Respuesta guardada; <c>null</c> cuando la pregunta aún no se ha respondido.</summary>
    public string? Respuesta { get; set; }

    public string? Observacion { get; set; }

    /// <summary>
    /// «¿Presenta novedad?» de esta pregunta (29/10/2026): true, novedad; false o null, no. En las firmadas
    /// antes de ese día el servidor lo deduce de la regla de entonces (respuesta «No»).
    /// </summary>
    public bool? PresentaNovedad { get; set; }

    /// <summary>
    /// true cuando el inspector marcó «¿Presenta novedad?» = Sí (regla 4 del SRS). Desde el 29/10/2026 la
    /// novedad ya no sale de responder «No» a la pregunta.
    /// </summary>
    public bool EsNovedad => PresentaNovedad == true;

    /// <summary>true cuando la pregunta es obligatoria y todavía no tiene respuesta (regla 3 del SRS).</summary>
    public bool FaltaResponder => Obligatoria && string.IsNullOrWhiteSpace(Respuesta);
}