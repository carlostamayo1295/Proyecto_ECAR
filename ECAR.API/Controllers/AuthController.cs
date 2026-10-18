using System.Security.Claims;
using ECAR.API.Configuration;
using ECAR.API.Exceptions;
using ECAR.API.Services;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login(LoginDto loginDto)
    {
        LoginResponseDto? result;
        try
        {
            result = await _authService.LoginAsync(loginDto);
        }
        catch (CuentaBloqueadaException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<LoginResponseDto>.ErrorResponse(ex.Message));
        }

        if (result == null)
        {
            return Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse("Credenciales inválidas"));
        }

        return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(result, "Login exitoso"));
    }

    [HttpPost("validate")]
    [PermitirCambioPasswordPendiente]
    public async Task<ActionResult<ApiResponse<bool>>> ValidateToken([FromBody] string token)
    {
        var isValid = await _authService.ValidateTokenAsync(token);

        if (!isValid)
        {
            return Unauthorized(ApiResponse<bool>.ErrorResponse("Token inválido"));
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Token válido"));
    }

    [HttpPost("cambiar-password")]
    [Authorize]
    [PermitirCambioPasswordPendiente]
    public async Task<ActionResult<ApiResponse<bool>>> CambiarPassword(CambiarPasswordDto cambio)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var idUsuario))
        {
            return Unauthorized(ApiResponse<bool>.ErrorResponse("Token inválido"));
        }

        List<string> errores;
        try
        {
            errores = await _authService.CambiarPasswordAsync(idUsuario, cambio);
        }
        catch (CuentaBloqueadaException ex)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<bool>.ErrorResponse(ex.Message));
        }

        if (errores.Count > 0)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse("No se pudo cambiar la contraseña", errores));
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true,
            "Contraseña cambiada exitosamente. Inicie sesión de nuevo con la nueva contraseña"));
    }

    [HttpGet("politica")]
    [AllowAnonymous]
    [PermitirCambioPasswordPendiente]
    public ActionResult<ApiResponse<PoliticaSeguridadDto>> GetPolitica([FromServices] IOptions<SeguridadOptions> opciones) =>
        Ok(ApiResponse<PoliticaSeguridadDto>.SuccessResponse(opciones.Value.ToDto()));
}
