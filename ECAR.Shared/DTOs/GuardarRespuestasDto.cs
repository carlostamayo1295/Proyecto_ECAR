using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Entrada de PUT /api/inspecciones/{id}/respuestas. Es un lote: la pantalla envía solo las
/// preguntas que cambiaron (guardado incremental) o todas al avanzar de paso.
/// El API hace upsert por (IdInspeccion, IdPregunta), así que reenviar lo mismo es inocuo.
/// </summary>
public class GuardarRespuestasDto
{
    [MinLength(1, ErrorMessage = "Debe enviar al menos una respuesta")]
    public List<RespuestaEjecucionDto> Respuestas { get; set; } = new();
}

/// <summary>
/// Una respuesta dentro del lote. No lleva IdInspeccion: va en la ruta, para que un lote no
/// pueda mezclar inspecciones.
/// </summary>
public class RespuestaEjecucionDto
{
    [Range(1, long.MaxValue, ErrorMessage = "La pregunta es requerida")]
    public long IdPregunta { get; set; }

    /// <summary>"Si" / "No" para preguntas Sí/No; texto libre para las de tipo Texto.</summary>
    [Required(ErrorMessage = "La respuesta es requerida")]
    public string Respuesta { get; set; } = string.Empty;

    /// <summary>Obligatoria cuando la respuesta reporta novedad (regla 4 del SRS).</summary>
    public string? Observacion { get; set; }
}
