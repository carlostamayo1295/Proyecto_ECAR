using BCrypt.Net;
using ECAR.API.Configuration;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ECARDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly SeguridadOptions _seguridad;

    public AuthController(IAuthService authService, ECARDbContext context, ICurrentUser currentUser,
        IOptions<SeguridadOptions> seguridad)
    {
        _authService = authService;
        _context = context;
        _currentUser = currentUser;
        _seguridad = seguridad.Value;
    }

    /// <summary>
    /// Endpoint 22. 401 con un mensaje genérico si las credenciales no valen y 423 si la cuenta
    /// está bloqueada; la respuesta dice además si hay que cambiar la contraseña antes de entrar.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login(LoginDto loginDto)
    {
        var resultado = await _authService.IniciarSesionAsync(loginDto);

        return resultado.Estado switch
        {
            EstadoCredencial.Valida => Ok(ApiResponse<LoginResponseDto>.SuccessResponse(resultado.Sesion!, "Login exitoso")),
            EstadoCredencial.Bloqueada => StatusCode(StatusCodes.Status423Locked,
                ApiResponse<LoginResponseDto>.ErrorResponse(resultado.MensajeBloqueo)),
            _ => Unauthorized(ApiResponse<LoginResponseDto>.ErrorResponse(MensajesCuenta.CredencialesIncorrectas))
        };
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

    /// <summary>Endpoint 24: reglas de contraseña y minutos de inactividad, para la pantalla.</summary>
    [HttpGet("politica")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<PoliticaSeguridadDto>> GetPolitica() =>
        Ok(ApiResponse<PoliticaSeguridadDto>.SuccessResponse(_seguridad.ComoPolitica()));

    /// <summary>
    /// Endpoint 23 (Parte 11 §11.300(b)). Exige la contraseña actual, aplica la política y el
    /// historial y renueva el SecurityStamp: los tokens anteriores, incluido el de esta
    /// petición, dejan de valer y hay que volver a iniciar sesión.
    /// </summary>
    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> CambiarPassword(CambiarPasswordDto dto)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.HistorialPasswords)
            .FirstOrDefaultAsync(u => u.IdUsuario == _currentUser.IdUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return Unauthorized(ApiResponse<bool>.ErrorResponse("Su sesión no es válida"));
        }

        if (string.IsNullOrWhiteSpace(usuario.PasswordHash))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                "Su cuenta entra con Active Directory: la contraseña se cambia en el directorio de ECAR"));
        }

        var credencial = await _authService.VerificarPasswordAsync(usuario.IdUsuario, dto.Actual, "CambioPassword");
        if (credencial.Estado == EstadoCredencial.Bloqueada)
        {
            return StatusCode(StatusCodes.Status423Locked, ApiResponse<bool>.ErrorResponse(credencial.MensajeBloqueo));
        }

        if (credencial.Estado != EstadoCredencial.Valida)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse("La contraseña actual no es correcta"));
        }

        var errores = PoliticaPassword.Validar(dto.Nueva, _seguridad);
        if (errores.Count > 0)
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(string.Join(". ", errores), errores));
        }

        if (PoliticaPassword.EstaRepetida(dto.Nueva, usuario, _seguridad))
        {
            return BadRequest(ApiResponse<bool>.ErrorResponse(
                $"La nueva contraseña no puede ser ninguna de sus últimas {_seguridad.PasswordsRecordadas}"));
        }

        var ahora = DateTime.UtcNow;
        usuario.HistorialPasswords.Add(new HistorialPassword { PasswordHash = usuario.PasswordHash, Fecha = ahora });
        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Nueva);
        usuario.FechaCambioPassword = ahora;
        usuario.DebeCambiarPassword = false;
        _context.RegistrarEvento(AuditoriaAcciones.CambioPassword, usuario.IdUsuario);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.SuccessResponse(true,
            "Contraseña cambiada. Inicie sesión de nuevo con la nueva contraseña."));
    }

    /// <summary>Deja constancia del cierre de sesión (evento CierreSesion de §3.3).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout([FromQuery] bool porInactividad = false)
    {
        await _context.RegistrarEventoAsync(AuditoriaAcciones.CierreSesion, _currentUser.IdUsuario,
            new { PorInactividad = porInactividad });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Sesión cerrada"));
    }
}
