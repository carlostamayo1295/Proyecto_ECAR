using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECAR.API.Services;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PermitirCambioPasswordPendienteAttribute : Attribute;

public sealed class CambioPasswordPendienteFilter : IAsyncActionFilter
{
    public const string Mensaje = "Debe cambiar su contraseña antes de continuar";

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var usuario = context.HttpContext.User;
        var pendiente = usuario.Identity?.IsAuthenticated == true &&
                        usuario.HasClaim(AuthService.ClaimCambioPasswordPendiente, "true");
        var permitido = context.ActionDescriptor.EndpointMetadata.OfType<PermitirCambioPasswordPendienteAttribute>().Any();

        if (pendiente && !permitido)
        {
            context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse(Mensaje))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return Task.CompletedTask;
        }

        return next();
    }
}
