using ECAR.Shared.Responses;

namespace ECAR.API.Services;

/// <summary>
/// Quien entra con una contraseña temporal (tras un restablecimiento) o caducada solo puede usar
/// /api/auth/* hasta cambiarla (PLAN_FASE4_TAREAS §3.8, Parte 11 §11.200(a)(3) y §11.300(b)):
/// así el Administrador que la restableció nunca puede trabajar con la cuenta de otro.
/// </summary>
public sealed class CambioPasswordObligatorio(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim(AuthService.ClaimDebeCambiarPassword, "1") &&
            context.Request.Path.StartsWithSegments("/api") &&
            !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse<bool>.ErrorResponse(
                "Debe cambiar su contraseña antes de continuar"));
            return;
        }

        await siguiente(context);
    }
}
