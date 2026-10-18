using BCrypt.Net;
using ECAR.API.Configuration;
using ECAR.API.Exceptions;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
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
    public const string OperacionLogin = "InicioSesion";
    public const string OperacionFirma = "Firma";
    public const string OperacionCambioPassword = "CambioPassword";

    public const string ClaimCambioPasswordPendiente = "ecar_cambio_password";

    private readonly ECARDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IActiveDirectoryAuthService _activeDirectoryAuthService;
    private readonly EcarAuthenticationOptions _authenticationOptions;
    private readonly PoliticaPasswords _politica;

    public AuthService(
        ECARDbContext context,
        IConfiguration configuration,
        IActiveDirectoryAuthService activeDirectoryAuthService,
        IOptions<EcarAuthenticationOptions> authenticationOptions,
        PoliticaPasswords? politica = null)
    {
        _context = context;
        _configuration = configuration;
        _activeDirectoryAuthService = activeDirectoryAuthService;
        _authenticationOptions = authenticationOptions.Value;
        _politica = politica ?? new PoliticaPasswords(Options.Create(new SeguridadOptions()));
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
            await _context.RegistrarEventoAsync(AuditoriaAcciones.LoginFallido, usuario?.IdUsuario ?? 0,
                new { Operacion = OperacionLogin, Identificador = identity, Detalle = "Usuario inexistente o inactivo" },
                usuario: identity, idUsuario: usuario?.IdUsuario);
            return null;
        }

        var autenticacion = await VerificarAsync(usuario, loginDto.Password, OperacionLogin, soloLocal: false);
        if (autenticacion == MetodoAutenticacion.Ninguno)
        {
            return null;
        }

        var esLocal = autenticacion == MetodoAutenticacion.Local;
        if (esLocal && usuario.FechaCambioPassword == null)
        {
            usuario.FechaCambioPassword = DateTime.UtcNow;
        }

        var debeCambiarPassword = esLocal && (usuario.DebeCambiarPassword || _politica.EstaCaducada(usuario));

        usuario.UltimoAcceso = DateTime.UtcNow;
        _context.RegistrarEvento(AuditoriaAcciones.LoginExitoso, usuario.IdUsuario,
            new { Metodo = autenticacion.ToString(), DebeCambiarPassword = debeCambiarPassword },
            usuario: Describir(usuario), idUsuario: usuario.IdUsuario);
        await _context.SaveChangesAsync();

        // Agregar al token los roles guardados.
        var roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();

        // Crear el JWT que se devuelve al cliente.
        var token = GenerateJwtToken(usuario, roles, debeCambiarPassword);

        return new LoginResponseDto
        {
            Token = token,
            Correo = usuario.Correo,
            Nombre = usuario.Nombre,
            Roles = roles,
            Expiration = DateTime.UtcNow.AddHours(GetJwtExpirationHours()),
            DebeCambiarPassword = debeCambiarPassword,
            PasswordExpiraEl = esLocal ? _politica.CalcularExpiracion(usuario) : null
        };
    }

    public async Task<bool> VerificarPasswordAsync(long idUsuario, string? password, string operacion)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return false;
        }

        return await VerificarAsync(usuario, password, operacion, soloLocal: false) != MetodoAutenticacion.Ninguno;
    }

    public async Task<List<string>> CambiarPasswordAsync(long idUsuario, CambiarPasswordDto cambio)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        if (usuario == null || !usuario.Activo)
        {
            return ["Usuario no encontrado"];
        }

        if (string.IsNullOrWhiteSpace(usuario.PasswordHash))
        {
            return ["La contraseña de esta cuenta la administra Active Directory"];
        }

        if (await VerificarAsync(usuario, cambio.Actual, OperacionCambioPassword, soloLocal: true) ==
            MetodoAutenticacion.Ninguno)
        {
            return ["La contraseña actual es incorrecta"];
        }

        var errores = _politica.Validar(cambio.Nueva);
        if (errores.Count > 0)
        {
            return errores;
        }

        if (await _politica.EsRecienteAsync(_context, usuario, cambio.Nueva))
        {
            return [$"La nueva contraseña no puede ser la actual ni ninguna de las últimas {_politica.Opciones.PasswordsRecordadas}"];
        }

        _politica.Establecer(_context, usuario, cambio.Nueva, esTemporal: false);
        _context.RegistrarEvento(AuditoriaAcciones.CambioPassword, usuario.IdUsuario);

        await _context.SaveChangesAsync();
        return [];
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

    private async Task<MetodoAutenticacion> VerificarAsync(Usuario usuario, string? password, string operacion,
        bool soloLocal)
    {
        var ahora = DateTime.UtcNow;
        if (usuario.BloqueadoHasta is { } bloqueadoHasta && bloqueadoHasta > ahora)
        {
            await _context.RegistrarEventoAsync(AuditoriaAcciones.LoginFallido, usuario.IdUsuario,
                new { Operacion = operacion, Detalle = "Cuenta bloqueada" },
                usuario: Describir(usuario), idUsuario: usuario.IdUsuario);
            throw new CuentaBloqueadaException(bloqueadoHasta);
        }

        var metodo = await AutenticarAsync(usuario, password ?? string.Empty, soloLocal);
        if (metodo != MetodoAutenticacion.Ninguno)
        {
            if (usuario.IntentosFallidos != 0 || usuario.BloqueadoHasta != null)
            {
                usuario.IntentosFallidos = 0;
                usuario.BloqueadoHasta = null;
                await _context.SaveChangesAsync();
            }

            return metodo;
        }

        var opciones = _politica.Opciones;
        usuario.IntentosFallidos++;
        _context.RegistrarEvento(AuditoriaAcciones.LoginFallido, usuario.IdUsuario,
            new { Operacion = operacion, Intento = usuario.IntentosFallidos, Maximo = opciones.MaxIntentosFallidos },
            usuario: Describir(usuario), idUsuario: usuario.IdUsuario);

        if (usuario.IntentosFallidos < opciones.MaxIntentosFallidos)
        {
            await _context.SaveChangesAsync();
            return MetodoAutenticacion.Ninguno;
        }

        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = ahora.AddMinutes(opciones.MinutosBloqueo);
        _context.RegistrarEvento(AuditoriaAcciones.CuentaBloqueada, usuario.IdUsuario,
            new { Operacion = operacion, usuario.BloqueadoHasta, Intentos = opciones.MaxIntentosFallidos },
            usuario: Describir(usuario), idUsuario: usuario.IdUsuario);
        await _context.SaveChangesAsync();
        throw new CuentaBloqueadaException(usuario.BloqueadoHasta.Value);
    }

    private async Task<MetodoAutenticacion> AutenticarAsync(Usuario usuario, string password, bool soloLocal)
    {
        var mode = _authenticationOptions.GetMode();

        if (soloLocal || mode is EcarAuthenticationMode.Local or EcarAuthenticationMode.Hybrid)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(usuario.PasswordHash) &&
                    BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
                {
                    return MetodoAutenticacion.Local;
                }
            }
            catch (SaltParseException)
            {
                // Un hash antiguo dañado debe fallar la autenticación en lugar de devolver HTTP 500.
            }
        }

        if (!soloLocal &&
            mode is EcarAuthenticationMode.ActiveDirectory or EcarAuthenticationMode.Hybrid &&
            !string.IsNullOrWhiteSpace(usuario.UsuarioAD) &&
            await _activeDirectoryAuthService.AuthenticateAsync(usuario.UsuarioAD, password))
        {
            return MetodoAutenticacion.ActiveDirectory;
        }

        return MetodoAutenticacion.Ninguno;
    }

    private string GenerateJwtToken(Usuario usuario, List<string> roles, bool debeCambiarPassword)
    {
        var jwtSecret = _configuration["JWT:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var key = Encoding.UTF8.GetBytes(jwtSecret);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new Claim(ClaimTypes.Email, usuario.Correo),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            // Si el sello cambia (desactivación, cambio de contraseña), el token deja de valer.
            new Claim(SelloSeguridad.TipoClaim, usuario.SecurityStamp)
        };

        if (debeCambiarPassword)
        {
            claims.Add(new Claim(ClaimCambioPasswordPendiente, "true"));
        }

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
        // 8 h, un turno (PLAN_FASE4_TAREAS §3.8); el cierre por inactividad lo hace el cliente.
        return int.TryParse(_configuration["JWT:ExpirationHours"], out var hours) ? hours : 8;
    }

    private static string Describir(Usuario usuario) => $"{usuario.Nombre} ({usuario.Correo})";

    private enum MetodoAutenticacion
    {
        Ninguno,
        Local,
        ActiveDirectory
    }
}
