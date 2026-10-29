using ECAR.Infrastructure.Entities;
using ECAR.Shared;

namespace ECAR.API.Services;

/// <summary>
/// Qué respuesta es una novedad (regla 4 del SRS). Desde el 29/10/2026 lo decide el «¿Presenta novedad?» que
/// marca el inspector en cada pregunta, no la respuesta: responder «No» ya no es novedad por sí solo.
/// </summary>
public static class ReglaNovedad
{
    /// <summary>
    /// «¿Presenta novedad?» para mostrar: lo que marcó el inspector. Las firmadas antes del 29/10/2026 (sello v0
    /// a v2) no tienen la marca: se deduce de la regla de entonces, una respuesta Sí/No «No». En una en curso sin
    /// marcar es null.
    /// </summary>
    public static bool? Presenta(Inspeccion inspeccion, PreguntaChecklist pregunta, RespuestaInspeccion? respuesta)
    {
        if (respuesta?.PresentaNovedad is { } marcada)
        {
            return marcada;
        }

        var reglaAnterior = VerificadorIntegridad.NombreAlgoritmo(inspeccion) is { } algoritmo
                            && algoritmo != VerificadorIntegridad.AlgoritmoHmacV3;
        return reglaAnterior && respuesta != null
            ? pregunta.TipoRespuesta == TiposRespuesta.SiNo
              && string.Equals(respuesta.Respuesta, "No", StringComparison.OrdinalIgnoreCase)
            : null;
    }
}
