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

    /// <summary>Respuesta guardada; <c>null</c> cuando la pregunta aún no se ha respondido.</summary>
    public string? Respuesta { get; set; }

    public string? Observacion { get; set; }

    /// <summary>true cuando es una pregunta Sí/No respondida con "No" (novedad, regla 4 del SRS).</summary>
    public bool EsNovedad =>
        TipoRespuesta == TiposRespuesta.SiNo &&
        string.Equals(Respuesta, "No", StringComparison.OrdinalIgnoreCase);

    /// <summary>true cuando la pregunta es obligatoria y todavía no tiene respuesta (regla 3 del SRS).</summary>
    public bool FaltaResponder => Obligatoria && string.IsNullOrWhiteSpace(Respuesta);
}
