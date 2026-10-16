using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);

    Task<bool> ValidateTokenAsync(string token);

    Task<bool> VerificarPasswordAsync(long idUsuario, string? password, string operacion);

    Task<List<string>> CambiarPasswordAsync(long idUsuario, CambiarPasswordDto cambio);
}
