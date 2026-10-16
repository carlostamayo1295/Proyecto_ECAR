using ECAR.API.Configuration;
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
    /// <summary>
    /// Va en el token de quien tiene una contraseña temporal o caducada: con él solo puede usar
    /// /api/auth/* hasta cambiarla (CambioPasswordObligatorio).
    /// </summary>
    public const string ClaimDebeCambiarPassword = "ecar_cambiar_password";

    private readonly ECARDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IActiveDirectoryAuthService _activeDirectoryAuthService;
    private readonly EcarAuthenticationOptions _authenticationOptions;
    private readonly SeguridadOptions _seguridad;

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
        _seguridad = SeguridadOptions.Desde(configuration);
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto) =>
        (await IniciarSesionAsync(loginDto)).Sesion;

    /// <summary>
    /// Inicio de sesión con bloqueo por intentos (endpoint 22, Parte 11 §11.300(d)): cada intento
    /// queda en la auditoría (LoginExitoso, LoginFallido, CuentaBloqueada) con el usuario que se
    /// tecleó, nunca con la contraseña. El motivo exacto de un fallo no se le dice al cliente.
    /// </summary>
    public async Task<ResultadoLogin> IniciarSesionAsync(LoginDto loginDto)
    {
        var identidad = loginDto.CorreoOrUsuarioAD.Trim();

        // Un usuario puede iniciar sesión con su correo o con su usuario de Active Directory.
        var usuario = await _context.Usuarios
            .Include(u => u.UsuarioRoles)
            .ThenInclude(ur => ur.Rol)
            .FirstOrDefaultAsync(u =>
                u.Correo == identidad ||
                u.UsuarioAD == identidad);

        var credencial = await VerificarAsync(usuario, loginDto.Password, identidad, contexto: "Login");
        if (credencial.Estado != EstadoCredencial.Valida)
        {
            return new ResultadoLogin(credencial.Estado, null, credencial.BloqueadoHasta);
        }

        var ahora = DateTime.UtcNow;
        usuario!.UltimoAcceso = ahora;
        if (credencial.Local && usuario.FechaCambioPassword == null)
        {
            // Cuentas anteriores a la Fase 4: la caducidad empieza a contar desde este acceso.
            usuario.FechaCambioPassword = ahora;
        }

        var expiraEl = credencial.Local ? PoliticaPassword.ExpiraEl(usuario, _seguridad) : null;
        var debeCambiar = credencial.Local && (usuario.DebeCambiarPassword || expiraEl <= ahora);

        _context.RegistrarEvento(AuditoriaAcciones.LoginExitoso, usuario.IdUsuario,
            new { Identidad = identidad, DebeCambiarPassword = debeCambiar },
            usuario: $"{usuario.Nombre} ({usuario.Correo})", idUsuario: usuario.IdUsuario);
        await _context.SaveChangesAsync();

        // Agregar al token los roles guardados.
        var roles = usuario.UsuarioRoles.Select(ur => ur.Rol.Nombre).ToList();

        return new ResultadoLogin(EstadoCredencial.Valida, new LoginResponseDto
        {
            Token = GenerateJwtToken(usuario, roles, debeCambiar),
            Correo = usuario.Correo,
            Nombre = usuario.Nombre,
            Roles = roles,
            Expiration = DateTime.UtcNow.AddHours(GetJwtExpirationHours()),
            DebeCambiarPassword = debeCambiar,
            PasswordExpiraEl = expiraEl
        }, null);
    }

    /// <summary>
    /// Reautenticación (endpoint 12 y 23): comprueba la contraseña de un usuario ya autenticado.
    /// Un fallo cuenta como intento fallido y puede bloquear la cuenta. Si es correcta, los
    /// cambios (contador a cero) quedan en el contexto y los guarda quien llama, junto con lo suyo.
    /// </summary>
    public async Task<ResultadoCredencial> VerificarPasswordAsync(long idUsuario, string? password, string contexto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdUsuario == idUsuario);
        return await VerificarAsync(usuario, password ?? string.Empty, usuario?.Correo ?? idUsuario.ToString(), contexto);
    }

    private async Task<ResultadoCredencial> VerificarAsync(Usuario? usuario, string password, string identidad, string contexto)
    {
        var ahora = DateTime.UtcNow;
        if (usuario == null || !usuario.Activo)
        {
            _context.RegistrarEvento(AuditoriaAcciones.LoginFallido, usuario?.IdUsuario ?? 0,
                new { Identidad = identidad, Contexto = contexto, Motivo = usuario == null ? "Usuario inexistente" : "Usuario inactivo" },
                usuario: identidad);
            await _context.SaveChangesAsync();
            return new ResultadoCredencial(EstadoCredencial.Invalida);
        }

        if (usuario.BloqueadoHasta > ahora)
        {
            _context.RegistrarEvento(AuditoriaAcciones.LoginFallido, usuario.IdUsuario,
                new { Identidad = identidad, Contexto = contexto, Motivo = "Cuenta bloqueada" }, usuario: identidad);
            await _context.SaveChangesAsync();
            return new ResultadoCredencial(EstadoCredencial.Bloqueada, usuario.BloqueadoHasta);
        }

        var (correcta, local) = await ComprobarCredencialAsync(usuario, password);
        if (correcta)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
            return new ResultadoCredencial(EstadoCredencial.Valida, Local: local);
        }

        usuario.IntentosFallidos++;
        if (usuario.IntentosFallidos >= _seguridad.MaxIntentosFallidos)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = ahora.AddMinutes(_seguridad.MinutosBloqueo);
            _context.RegistrarEvento(AuditoriaAcciones.CuentaBloqueada, usuario.IdUsuario,
                new { Identidad = identidad, Contexto = contexto, usuario.BloqueadoHasta }, usuario: identidad);
            await _context.SaveChangesAsync();
            return new ResultadoCredencial(EstadoCredencial.Bloqueada, usuario.BloqueadoHasta);
        }

        _context.RegistrarEvento(AuditoriaAcciones.LoginFallido, usuario.IdUsuario,
            new { Identidad = identidad, Contexto = contexto, Motivo = "Contraseña incorrecta", usuario.IntentosFallidos },
            usuario: identidad);
        await _context.SaveChangesAsync();
        return new ResultadoCredencial(EstadoCredencial.Invalida);
    }

    /// <summary>Contraseña local (BCrypt) o bind contra Active Directory, según el modo.</summary>
    private async Task<(bool Correcta, bool Local)> ComprobarCredencialAsync(Usuario usuario, string password)
    {
        var mode = _authenticationOptions.GetMode();
        if (mode is EcarAuthenticationMode.Local or EcarAuthenticationMode.Hybrid &&
            PoliticaPassword.CoincideSinExcepcion(password, usuario.PasswordHash))
        {
            return (true, true);
        }

        if (mode is EcarAuthenticationMode.ActiveDirectory or EcarAuthenticationMode.Hybrid &&
            !string.IsNullOrWhiteSpace(usuario.UsuarioAD) &&
            await _activeDirectoryAuthService.AuthenticateAsync(usuario.UsuarioAD, password))
        {
            return (true, false);
        }

        return (false, false);
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
            claims.Add(new Claim(ClaimDebeCambiarPassword, "1"));
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
}
