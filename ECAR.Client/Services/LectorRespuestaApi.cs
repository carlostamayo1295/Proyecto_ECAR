using ECAR.Shared.Responses;
using System.Net;
using System.Text.Json;

namespace ECAR.Client.Services;

/// <summary>
/// Lee cualquier respuesta del API de la misma forma: un ApiResponse, un ProblemDetails de
/// validación ([ApiController]) o, si no hay cuerpo, un mensaje según el código HTTP. Nunca
/// devuelve null y el mensaje siempre se puede mostrar tal cual. Lo usan HttpClientService
/// (métodos de la Fase 4) y AuthService (login).
/// </summary>
public static class LectorRespuestaApi
{
    public const string MensajeSinConexion =
        "No se pudo conectar con el servidor. Compruebe la conexión e intente de nuevo.";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    /// <param name="mensajeSegunEstado">
    /// Para cuando un código significa otra cosa en ese endpoint, p. ej. un 401 en el login es una
    /// contraseña incorrecta y no una sesión vencida. Por defecto, <see cref="MensajeSegunEstado"/>.
    /// </param>
    public static async Task<ApiResponse<T>> LeerAsync<T>(HttpResponseMessage response,
        Func<HttpStatusCode, string>? mensajeSegunEstado = null)
    {
        var mensaje = mensajeSegunEstado ?? MensajeSegunEstado;
        var texto = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                using var documento = JsonDocument.Parse(texto);
                var raiz = documento.RootElement;
                if (raiz.ValueKind == JsonValueKind.Object)
                {
                    if (raiz.TryGetProperty("success", out _))
                    {
                        var api = raiz.Deserialize<ApiResponse<T>>(OpcionesJson);
                        if (api != null)
                        {
                            if (!api.Success && string.IsNullOrWhiteSpace(api.Message))
                            {
                                api.Message = mensaje(response.StatusCode);
                            }

                            return api;
                        }
                    }

                    if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
                    {
                        var lista = errores.EnumerateObject()
                            .SelectMany(campo => campo.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty))
                            .Where(e => e.Length > 0)
                            .ToList();
                        return ApiResponse<T>.ErrorResponse(
                            lista.Count > 0 ? string.Join(" ", lista) : mensaje(response.StatusCode), lista);
                    }
                }
            }
            catch (JsonException)
            {
                // Cuerpo que no es JSON (p. ej. una página de error): se usa el código HTTP.
            }
        }

        return ApiResponse<T>.ErrorResponse(mensaje(response.StatusCode));
    }

    public static string MensajeSegunEstado(HttpStatusCode estado) => (int)estado switch
    {
        400 => "La solicitud no es válida.",
        401 => "Su sesión no es válida. Inicie sesión de nuevo.",
        403 => "No tiene permiso para realizar esta acción.",
        404 => "Esta función todavía no está disponible en el servidor.",
        405 => "El servidor ya no admite esta acción.",
        409 => "La acción no es posible en el estado actual del registro.",
        423 => "La cuenta está bloqueada temporalmente.",
        _ => $"El servidor respondió con un error ({(int)estado})."
    };
}
