using System.Net;

namespace ECAR.Client.Services;

/// <summary>
/// Intercepta las respuestas del API: si una petición que llevaba token recibe 401, la sesión
/// ya no vale (venció o el servidor la invalidó) y se manda al login conservando la página.
///
/// Solo actúa cuando la petición llevaba token. Un 401 sin token es el de una pantalla a la que
/// se entró sin sesión, y eso lo resuelven las guardas de ruta. Tampoco actúa sobre api/auth:
/// ahí un 401 significa credenciales incorrectas, no sesión vencida.
/// </summary>
public class SesionExpiradaHandler : DelegatingHandler
{
    private readonly SesionService _sesion;

    public SesionExpiradaHandler(SesionService sesion)
    {
        _sesion = sesion;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized
            && request.Headers.Authorization is not null
            && !EsDeAutenticacion(request))
        {
            await _sesion.IrAlLoginAsync(expirada: true);
        }

        return response;
    }

    private static bool EsDeAutenticacion(HttpRequestMessage request) =>
        request.RequestUri?.AbsolutePath.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) == true;
}
