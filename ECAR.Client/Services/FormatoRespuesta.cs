namespace ECAR.Client.Services;

/// <summary>
/// Texto de una respuesta para mostrarla en pantalla o en el informe impreso.
/// El API guarda las respuestas Sí/No como "Si" y "No", sin tilde (contrato de la Fase 3);
/// la tilde se pone solo al mostrarlas.
/// </summary>
public static class FormatoRespuesta
{
    public static string Mostrar(string? respuesta, string sinResponder = "Sin responder")
    {
        if (string.IsNullOrWhiteSpace(respuesta))
        {
            return sinResponder;
        }

        return string.Equals(respuesta.Trim(), "Si", StringComparison.OrdinalIgnoreCase) ? "Sí" : respuesta;
    }
}
