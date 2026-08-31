using ECAR.API.Services;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

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
        var result = await _authService.LoginAsync(loginDto);

        if (result == null)
        {
            return Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse("Credenciales inválidas"));
        }

        return Ok(ApiResponse<LoginResponseDto>.SuccessResponse(result, "Login exitoso"));
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ApiResponse<bool>>> ValidateToken([FromBody] string token)
    {
        var isValid = await _authService.ValidateTokenAsync(token);

        if (!isValid)
        {
            return Unauthorized(ApiResponse<bool>.ErrorResponse("Token inválido"));
        }

        return Ok(ApiResponse<bool>.SuccessResponse(true, "Token válido"));
    }
}