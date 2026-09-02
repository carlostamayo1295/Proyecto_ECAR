using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Pruebas del núcleo de autenticación JWT (login local y validación de token).
/// Portadas desde la rama feature/jwt-auth-roles-authorization y adaptadas al
/// constructor actual de <see cref="AuthService"/> (modo Local, sin Active Directory).
/// </summary>
public class AuthServiceTests
{
    private const string ValidPassword = "Password123!";
    private const string JwtSecret = "una-clave-secreta-de-prueba-suficientemente-larga-32b";
    private const string JwtIssuer = "ECAR-Auditoria-Test";
    private const string JwtAudience = "ECAR-Users-Test";

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ECARDbContext(options);
    }

    private static IConfiguration CreateConfiguration(string? expirationHours = "24")
    {
        var settings = new Dictionary<string, string?>
        {
            ["JWT:Secret"] = JwtSecret,
            ["JWT:Issuer"] = JwtIssuer,
            ["JWT:Audience"] = JwtAudience,
            ["JWT:ExpirationHours"] = expirationHours
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    // Adaptador AD que nunca autentica: los tests corren en modo Local.
    private sealed class NullActiveDirectoryAuthService : IActiveDirectoryAuthService
    {
        public Task<bool> AuthenticateAsync(string usuarioAd, string password, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static AuthService CreateAuthService(ECARDbContext context, IConfiguration? configuration = null)
        => new(
            context,
            configuration ?? CreateConfiguration(),
            new NullActiveDirectoryAuthService(),
            Options.Create(new EcarAuthenticationOptions { Mode = nameof(EcarAuthenticationMode.Local) }));

    private static async Task<Usuario> SeedUsuarioAsync(ECARDbContext context, bool activo = true, params string[] roleNames)
    {
        var usuario = new Usuario
        {
            Nombre = "Usuario de Prueba",
            Correo = "prueba@ecar.com",
            UsuarioAD = "prueba.ad",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
            Activo = activo
        };

        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        foreach (var roleName in roleNames)
        {
            var rol = new Rol { Nombre = roleName };
            context.Roles.Add(rol);
            await context.SaveChangesAsync();

            context.UsuarioRoles.Add(new UsuarioRol { IdUsuario = usuario.IdUsuario, IdRol = rol.IdRol });
        }

        await context.SaveChangesAsync();

        return usuario;
    }

    [Fact]
    public async Task LoginAsync_ConCredencialesCorrectas_DevuelveTokenValido()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: true, "Administrador");

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba@ecar.com",
            Password = ValidPassword
        });

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.Token));
        Assert.Equal("prueba@ecar.com", result.Correo);
        Assert.True(result.Expiration > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ConUsuarioAD_DevuelveTokenValido()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: true, "Técnico");

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba.ad",
            Password = ValidPassword
        });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoginAsync_ConPasswordIncorrecta_DevuelveNull()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: true, "Administrador");

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba@ecar.com",
            Password = "PasswordIncorrecta"
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_ConUsuarioInexistente_DevuelveNull()
    {
        await using var context = CreateContext();

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "no-existe@ecar.com",
            Password = ValidPassword
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_ConUsuarioInactivo_DevuelveNull()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: false, "Administrador");

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba@ecar.com",
            Password = ValidPassword
        });

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_IncluyeTodosLosRolesDelUsuarioEnLaRespuestaYEnElToken()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: true, "Administrador", "Auditor");

        var service = CreateAuthService(context);

        var result = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba@ecar.com",
            Password = ValidPassword
        });

        Assert.NotNull(result);
        Assert.Equal(new[] { "Administrador", "Auditor" }, result!.Roles.OrderBy(r => r).ToArray());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        var roleClaims = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).OrderBy(r => r).ToArray();

        Assert.Equal(new[] { "Administrador", "Auditor" }, roleClaims);
    }

    [Fact]
    public async Task ValidateTokenAsync_ConTokenValido_DevuelveTrue()
    {
        await using var context = CreateContext();
        await SeedUsuarioAsync(context, activo: true, "Administrador");

        var service = CreateAuthService(context);

        var loginResult = await service.LoginAsync(new LoginDto
        {
            CorreoOrUsuarioAD = "prueba@ecar.com",
            Password = ValidPassword
        });

        Assert.NotNull(loginResult);

        var isValid = await service.ValidateTokenAsync(loginResult!.Token);

        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_ConTokenMalFormado_DevuelveFalse()
    {
        await using var context = CreateContext();
        var service = CreateAuthService(context);

        var isValid = await service.ValidateTokenAsync("token-invalido-no-jwt");

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_ConTokenFirmadoConOtraClave_DevuelveFalse()
    {
        await using var context = CreateContext();
        var service = CreateAuthService(context);

        var otraClave = Encoding.UTF8.GetBytes("otra-clave-secreta-completamente-distinta-32b");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(otraClave), SecurityAlgorithms.HmacSha256);
        var tokenAjeno = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: new[] { new Claim(ClaimTypes.Name, "Intruso") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenAjeno);

        var isValid = await service.ValidateTokenAsync(tokenString);

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_ConTokenVencido_DevuelveFalse()
    {
        await using var context = CreateContext();
        var service = CreateAuthService(context);

        var key = Encoding.UTF8.GetBytes(JwtSecret);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        var tokenVencido = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: new[] { new Claim(ClaimTypes.Name, "Usuario Vencido") },
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenVencido);

        var isValid = await service.ValidateTokenAsync(tokenString);

        Assert.False(isValid);
    }
}
