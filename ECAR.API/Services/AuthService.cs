using BCrypt.Net;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ECAR.API.Services;

public class AuthService : IAuthService
{
    private readonly ECARDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IActiveDirectoryAuthService _activeDirectoryAuthService;
    private readonly EcarAuthenticationOptions _authenticationOptions;

    public AuthService(
        ECARDbContext context,
        IConfiguration configuration,
        IActiveDirectoryAuthService activeDirectoryAuthService,
        IOptions<EcarAuthenticationOptions> authenticationOptions)
    {
        _context = context;
        _configuration = configuration;
        _activeDirectoryAuthService = activeDirectoryAuthService;
        _authenticationOptions = authenticationOptions.Value;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
    {
        var identity = loginDto.CorreoOrUsuarioAD.Trim();

        // Un usuario puede iniciar sesión con su correo o con su usuario de Active Directory.
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u =>
                u.Correo == identity ||
                u.UsuarioAD == identity);

        if (usuario == null || !usuario.Activo)
        {
            return null;
        }

        var mode = _authenticationOptions.GetMode();
        var localAuthenticationSucceeded = false;
        var activeDirectoryAuthenticationSucceeded = false;

        if (mode is EcarAuthenticationMode.Local or EcarAuthenticationMode.Hybrid)
        {
            try
            {
                localAuthenticationSucceeded = !string.IsNullOrWhiteSpace(usuario.PasswordHash) &&
                                               BCrypt.Net.BCrypt.Verify(loginDto.Password, usuario.PasswordHash);
            }
            catch (SaltParseException)
            {
                // Un hash antiguo dañado debe fallar la autenticación en lugar de devolver HTTP 500.
                localAuthenticationSucceeded = false;
            }
        }

        if (!localAuthenticationSucceeded &&
            (mode is EcarAuthenticationMode.ActiveDirectory or EcarAuthenticationMode.Hybrid) &&
            !string.IsNullOrWhiteSpace(usuario.UsuarioAD))
        {
            activeDirectoryAuthenticationSucceeded = await _activeDirectoryAuthService.AuthenticateAsync(
                usuario.UsuarioAD,
                loginDto.Password);
        }

        if (!localAuthenticationSucceeded && !activeDirectoryAuthenticationSucceeded)
        {
            return null;
        }

        // Agregar al token los roles guardados.
        var roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();

        // Crear el JWT que se devuelve al cliente.
        var token = GenerateJwtToken(usuario, roles);

        return new LoginResponseDto
        {
            Token = token,
            Correo = usuario.Correo,
            Nombre = usuario.Nombre,
            Roles = roles,
            Expiration = DateTime.UtcNow.AddHours(GetJwtExpirationHours())
        };
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSecret = _configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
            var key = Encoding.UTF8.GetBytes(jwtSecret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["JWT:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["JWT:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private string GenerateJwtToken(Usuario usuario, List<string> roles)
    {
        var jwtSecret = _configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = Encoding.UTF8.GetBytes(jwtSecret);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new Claim(ClaimTypes.Email, usuario.Correo),
            new Claim(ClaimTypes.Name, usuario.Nombre)
        };

        // ASP.NET lee estos claims cuando un endpoint exige un rol.
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(GetJwtExpirationHours()),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int GetJwtExpirationHours()
    {
        return int.TryParse(_configuration["JWT:ExpirationHours"], out var hours) ? hours : 24;
    }
}
