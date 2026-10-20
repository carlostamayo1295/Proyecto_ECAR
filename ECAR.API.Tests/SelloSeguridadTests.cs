using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Revocación de tokens por SecurityStamp (endpoint 27, Parte 11 §11.300(c)) y contexto de la
/// auditoría tomado de la petición HTTP.
/// </summary>
public class SelloSeguridadTests
{
    private const string Password = "Clave-de-prueba-1";

    private static ECARDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Usuario> CrearUsuarioAsync(ECARDbContext db)
    {
        var usuario = new Usuario
        {
            Nombre = "Beto Técnico",
            Correo = "beto@ecar.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password)
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static ClaimsPrincipal Principal(long idUsuario, string? sello)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, idUsuario.ToString()) };
        if (sello is not null)
        {
            claims.Add(new Claim(SelloSeguridad.TipoClaim, sello));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    [Fact]
    public async Task El_token_vale_mientras_el_usuario_este_activo_y_su_sello_no_cambie()
    {
        await using var db = CrearContexto();
        var usuario = await CrearUsuarioAsync(db);
        var token = Principal(usuario.IdUsuario, usuario.SecurityStamp);

        Assert.True(await SelloSeguridad.EsVigenteAsync(db, token));

        usuario.Nombre = "Beto Técnico Senior"; // otros cambios no revocan
        await db.SaveChangesAsync();
        Assert.True(await SelloSeguridad.EsVigenteAsync(db, token));
    }

    [Fact]
    public async Task Desactivar_al_usuario_invalida_sus_tokens_al_momento()
    {
        await using var db = CrearContexto();
        var usuario = await CrearUsuarioAsync(db);
        var token = Principal(usuario.IdUsuario, usuario.SecurityStamp);

        usuario.Activo = false;
        await db.SaveChangesAsync();
        Assert.False(await SelloSeguridad.EsVigenteAsync(db, token));

        // Reactivarlo no resucita el token viejo: hay que volver a iniciar sesión.
        usuario.Activo = true;
        await db.SaveChangesAsync();
        Assert.False(await SelloSeguridad.EsVigenteAsync(db, token));
        Assert.True(await SelloSeguridad.EsVigenteAsync(db, Principal(usuario.IdUsuario, usuario.SecurityStamp)));
    }

    [Fact]
    public async Task Cambiar_la_contrasena_invalida_sus_tokens()
    {
        await using var db = CrearContexto();
        var usuario = await CrearUsuarioAsync(db);
        var token = Principal(usuario.IdUsuario, usuario.SecurityStamp);

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Otra-clave-de-prueba-2");
        await db.SaveChangesAsync();

        Assert.False(await SelloSeguridad.EsVigenteAsync(db, token));
    }

    [Fact]
    public async Task Un_token_sin_sello_o_de_un_usuario_que_no_existe_no_vale()
    {
        await using var db = CrearContexto();
        var usuario = await CrearUsuarioAsync(db);

        Assert.False(await SelloSeguridad.EsVigenteAsync(db, Principal(usuario.IdUsuario, null)));
        Assert.False(await SelloSeguridad.EsVigenteAsync(db, Principal(usuario.IdUsuario, "otro-sello")));
        Assert.False(await SelloSeguridad.EsVigenteAsync(db, Principal(999, usuario.SecurityStamp)));
        Assert.False(await SelloSeguridad.EsVigenteAsync(db, null));
    }

    [Fact]
    public async Task El_login_emite_el_sello_actual_en_el_token_y_dura_8_horas_por_defecto()
    {
        await using var db = CrearContexto();
        var usuario = await CrearUsuarioAsync(db);
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT:Secret"] = "una-clave-secreta-de-prueba-suficientemente-larga-32b",
            ["JWT:Issuer"] = "ECAR-Auditoria-Test",
            ["JWT:Audience"] = "ECAR-Users-Test"
        }).Build();
        var servicio = new AuthService(db, configuracion, new SinActiveDirectory(),
            Options.Create(new EcarAuthenticationOptions { Mode = nameof(EcarAuthenticationMode.Local) }));

        var respuesta = await servicio.LoginAsync(new LoginDto { CorreoOrUsuarioAD = "beto@ecar.com", Password = Password });

        Assert.NotNull(respuesta);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(respuesta!.Token);
        Assert.Equal(usuario.SecurityStamp, jwt.Claims.Single(c => c.Type == SelloSeguridad.TipoClaim).Value);
        Assert.InRange(jwt.ValidTo - DateTime.UtcNow, TimeSpan.FromHours(7.9), TimeSpan.FromHours(8.1));
    }

    private sealed class SinActiveDirectory : IActiveDirectoryAuthService
    {
        public Task<bool> AuthenticateAsync(string usuarioAd, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    [Fact]
    public void El_contexto_de_auditoria_toma_usuario_ip_agente_y_motivo_de_la_peticion()
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "12"),
            new Claim(ClaimTypes.Name, "Ana Admin"),
            new Claim(ClaimTypes.Email, "ana@ecar.com")
        ], "Bearer"));
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.20");
        http.Request.Headers.UserAgent = "Mozilla/5.0 (Android)";
        var motivo = new MotivoCambio();
        motivo.Establecer("  Equipo dado de baja por calibración vencida  ");

        var contexto = new ContextoAuditoriaHttp(new HttpContextAccessor { HttpContext = http }, motivo);

        Assert.Equal(12, contexto.IdUsuario);
        Assert.Equal("Ana Admin (ana@ecar.com)", contexto.Usuario);
        Assert.Equal("192.168.1.20", contexto.DireccionIp);
        Assert.Equal("Mozilla/5.0 (Android)", contexto.AgenteUsuario);
        Assert.Equal("Equipo dado de baja por calibración vencida", contexto.Motivo);
    }

    [Fact]
    public void Sin_peticion_el_autor_es_Sistema_y_sin_token_es_Anonimo()
    {
        var motivo = new MotivoCambio();
        Assert.Equal("Sistema", new ContextoAuditoriaHttp(new HttpContextAccessor(), motivo).Usuario);

        var anonimo = new ContextoAuditoriaHttp(new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, motivo);
        Assert.Equal("Anónimo", anonimo.Usuario);
        Assert.Null(anonimo.IdUsuario);
        Assert.Null(anonimo.AgenteUsuario);
    }
}
