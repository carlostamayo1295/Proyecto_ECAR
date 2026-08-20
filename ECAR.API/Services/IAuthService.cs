using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IAuthService
{
    Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
    Task<bool> ValidateTokenAsync(string token);
}