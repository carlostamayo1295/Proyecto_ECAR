using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ECAR.Client.Services;

public class AuthorizationService
{
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "auth_token";

    public AuthorizationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<string>> GetUserRolesAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return new List<string>();
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            return roleClaims;
        }
        catch
        {
            return new List<string>();
        }
    }

    public async Task<bool> IsAdminAsync()
    {
        var roles = await GetUserRolesAsync();
        return roles.Contains("Administrador");
    }

    public async Task<bool> HasRoleAsync(string role)
    {
        var roles = await GetUserRolesAsync();
        return roles.Contains(role);
    }

    public async Task<string?> GetUserNameAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            return nameClaim?.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetUserEmailAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            return emailClaim?.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetUserIdAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
        catch
        {
            return null;
        }
    }
}