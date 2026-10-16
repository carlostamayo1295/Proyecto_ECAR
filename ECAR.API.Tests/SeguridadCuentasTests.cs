using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ECAR.API.Configuration;
using ECAR.API.Controllers;
using ECAR.API.Exceptions;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

public class SeguridadCuentasTests
{
    private const string Correo = "cuenta@ecar.test";

    private sealed class ContextoPruebas(IMotivoCambio motivo) : IContextoAuditoria
    {
        public long? IdUsuario => 1;
        public string Usuario => "Administrador (admin@ecar.test)";
        public string? DireccionIp => "127.0.0.1";
        public string? AgenteUsuario => "pruebas";
        public string? Motivo => motivo.Motivo;
    }

    private static (ECARDbContext Context, MotivoCambio Motivo) CrearContexto()
    {
        var motivo = new MotivoCambio();
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return (new ECARDbContext(options, new ContextoPruebas(motivo)), motivo);
    }

    private static async Task<Usuario> CrearUsuarioAsync(ECARDbContext context)
    {
        var rol = new Rol { Nombre = "Técnico" };
        var usuario = new Usuario
        {
            Nombre = "Cuenta de Prueba",
            Correo = Correo,
            PasswordHash = FirmaPruebas.PasswordHash,
            FechaCambioPassword = DateTime.UtcNow,
            Activo = true
        };
        usuario.UsuarioRoles.Add(new UsuarioRol { Rol = rol });
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        return usuario;
    }

    private static LoginDto Login(string password) => new() { CorreoOrUsuarioAD = Correo, Password = password };

    private static UsuariosController ControladorUsuarios(ECARDbContext context) =>
        new(context, new PoliticaPasswords(Options.Create(new SeguridadOptions())));

    [Fact]
    public async Task CincoIntentosFallidos_BloqueanLaCuentaYDevuelven423()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var controller = new AuthController(FirmaPruebas.CrearAuthService(context));

        for (var intento = 1; intento <= 4; intento++)
        {
            var fallido = await controller.Login(Login("incorrecta" + intento));
            Assert.IsType<UnauthorizedObjectResult>(fallido.Result);
        }

        var quinto = await controller.Login(Login("incorrecta5"));
        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(quinto.Result).StatusCode);

        var correcta = await controller.Login(Login(FirmaPruebas.Password));
        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(correcta.Result).StatusCode);

        var guardado = await context.Usuarios.AsNoTracking().SingleAsync(u => u.IdUsuario == usuario.IdUsuario);
        Assert.True(guardado.BloqueadoHasta > DateTime.UtcNow.AddMinutes(29));

        var acciones = await context.Auditoria.AsNoTracking().Select(a => a.Accion).ToListAsync();
        Assert.Equal(6, acciones.Count(a => a == AuditoriaAcciones.LoginFallido));
        Assert.Single(acciones, AuditoriaAcciones.CuentaBloqueada);
    }

    [Fact]
    public async Task UnIngresoCorrecto_ReiniciaLosIntentosFallidos()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        await CrearUsuarioAsync(context);
        var servicio = FirmaPruebas.CrearAuthService(context);

        for (var intento = 0; intento < 4; intento++)
        {
            Assert.Null(await servicio.LoginAsync(Login("incorrecta")));
        }

        Assert.NotNull(await servicio.LoginAsync(Login(FirmaPruebas.Password)));
        Assert.Null(await servicio.LoginAsync(Login("incorrecta")));
        Assert.Equal(1, (await context.Usuarios.AsNoTracking().SingleAsync()).IntentosFallidos);
    }

    [Fact]
    public async Task Desbloquear_ExigeMotivoQuedaAuditadoYPermiteEntrar()
    {
        var (context, motivo) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var servicio = FirmaPruebas.CrearAuthService(context);
        for (var intento = 0; intento < 4; intento++)
        {
            await servicio.LoginAsync(Login("incorrecta"));
        }

        await Assert.ThrowsAsync<CuentaBloqueadaException>(() => servicio.LoginAsync(Login("incorrecta")));
        context.ChangeTracker.Clear();

        var resultado = await ControladorUsuarios(context).DesbloquearUsuario(usuario.IdUsuario,
            new MotivoDto { Motivo = "El usuario confirmó su identidad por teléfono" }, motivo);

        Assert.IsType<OkObjectResult>(resultado.Result);
        var fila = await context.Auditoria.AsNoTracking()
            .SingleAsync(a => a.Tabla == "Usuarios" && a.Accion == AuditoriaAcciones.Modificar && a.Motivo != null);
        Assert.Equal("El usuario confirmó su identidad por teléfono", fila.Motivo);
        Assert.Contains(nameof(Usuario.BloqueadoHasta), fila.ValorNuevo);

        context.ChangeTracker.Clear();
        Assert.NotNull(await servicio.LoginAsync(Login(FirmaPruebas.Password)));
    }

    [Fact]
    public async Task Desbloquear_UnaCuentaSinBloqueoDevuelve400()
    {
        var (context, motivo) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);

        var resultado = await ControladorUsuarios(context).DesbloquearUsuario(usuario.IdUsuario,
            new MotivoDto { Motivo = "Solicitud del área de calidad" }, motivo);

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task CambiarPassword_AplicaLaPoliticaYNoPermiteReutilizarRecientes()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var servicio = FirmaPruebas.CrearAuthService(context);

        Assert.Contains("La contraseña actual es incorrecta",
            await servicio.CambiarPasswordAsync(usuario.IdUsuario, new CambiarPasswordDto { Actual = "otra", Nueva = "NuevaClave2026" }));
        Assert.NotEmpty(await servicio.CambiarPasswordAsync(usuario.IdUsuario,
            new CambiarPasswordDto { Actual = FirmaPruebas.Password, Nueva = "corta1" }));
        Assert.NotEmpty(await servicio.CambiarPasswordAsync(usuario.IdUsuario,
            new CambiarPasswordDto { Actual = FirmaPruebas.Password, Nueva = "SoloLetrasLargas" }));
        Assert.NotEmpty(await servicio.CambiarPasswordAsync(usuario.IdUsuario,
            new CambiarPasswordDto { Actual = FirmaPruebas.Password, Nueva = FirmaPruebas.Password }));

        var selloAnterior = (await context.Usuarios.AsNoTracking().SingleAsync()).SecurityStamp;
        Assert.Empty(await servicio.CambiarPasswordAsync(usuario.IdUsuario,
            new CambiarPasswordDto { Actual = FirmaPruebas.Password, Nueva = "NuevaClave2026" }));

        var cambiado = await context.Usuarios.AsNoTracking().SingleAsync();
        Assert.NotEqual(selloAnterior, cambiado.SecurityStamp);
        Assert.Single(await context.HistorialPasswords.AsNoTracking().ToListAsync());
        Assert.Contains(await context.Auditoria.AsNoTracking().ToListAsync(),
            a => a.Accion == AuditoriaAcciones.CambioPassword && a.RegistroId == usuario.IdUsuario);

        var reutilizada = await servicio.CambiarPasswordAsync(usuario.IdUsuario,
            new CambiarPasswordDto { Actual = "NuevaClave2026", Nueva = FirmaPruebas.Password });
        Assert.Contains(reutilizada, e => e.Contains("últimas"));
    }

    [Fact]
    public async Task CambiarPassword_ConActualIncorrectaDevuelve400()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var controller = new AuthController(FirmaPruebas.CrearAuthService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString())], "Pruebas"))
                }
            }
        };

        var resultado = await controller.CambiarPassword(new CambiarPasswordDto { Actual = "mala", Nueva = "NuevaClave2026" });

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task Restablecer_EntregaUnaTemporalUnaSolaVezYExigeCambiarlaAlEntrar()
    {
        var (context, motivo) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var selloAnterior = usuario.SecurityStamp;
        var controller = ControladorUsuarios(context);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var resultado = await controller.RestablecerPassword(usuario.IdUsuario,
            new MotivoDto { Motivo = "El usuario olvidó su contraseña" }, motivo);

        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        var temporal = Assert.IsType<ApiResponse<PasswordTemporalDto>>(ok.Value).Data!;
        Assert.True(temporal.DebeCambiarAlEntrar);
        Assert.Empty(new PoliticaPasswords(Options.Create(new SeguridadOptions())).Validar(temporal.PasswordTemporal));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());

        var guardado = await context.Usuarios.AsNoTracking().SingleAsync();
        Assert.True(guardado.DebeCambiarPassword);
        Assert.NotEqual(selloAnterior, guardado.SecurityStamp);
        Assert.DoesNotContain(temporal.PasswordTemporal, guardado.PasswordHash);

        var auditoria = await context.Auditoria.AsNoTracking().ToListAsync();
        Assert.DoesNotContain(auditoria, a => (a.ValorNuevo ?? string.Empty).Contains(temporal.PasswordTemporal));
        Assert.Contains(auditoria, a => a.Accion == AuditoriaAcciones.RestablecerPassword && a.Motivo == "El usuario olvidó su contraseña");

        context.ChangeTracker.Clear();
        var login = await FirmaPruebas.CrearAuthService(context).LoginAsync(Login(temporal.PasswordTemporal));
        Assert.NotNull(login);
        Assert.True(login!.DebeCambiarPassword);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.Token);
        Assert.Contains(token.Claims, c => c.Type == AuthService.ClaimCambioPasswordPendiente && c.Value == "true");
    }

    [Fact]
    public async Task PasswordCaducada_ObligaACambiarla()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        usuario.FechaCambioPassword = DateTime.UtcNow.AddDays(-91);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var login = await FirmaPruebas.CrearAuthService(context).LoginAsync(Login(FirmaPruebas.Password));

        Assert.True(login!.DebeCambiarPassword);
        Assert.True(login.PasswordExpiraEl < DateTime.UtcNow);
    }

    [Fact]
    public async Task Desactivar_InvalidaLasSesionesAnteriores()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;
        var usuario = await CrearUsuarioAsync(context);
        var admin = new Usuario { Nombre = "Admin", Correo = "admin@ecar.test", PasswordHash = "x", Activo = true };
        admin.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = "Administrador" } });
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();
        var selloAnterior = usuario.SecurityStamp;

        await ControladorUsuarios(context).DeleteUsuario(usuario.IdUsuario);

        Assert.NotEqual(selloAnterior, (await context.Usuarios.AsNoTracking().SingleAsync(u => u.IdUsuario == usuario.IdUsuario)).SecurityStamp);
    }

    [Fact]
    public async Task CrearUsuario_RechazaUnaPasswordQueNoCumpleLaPolitica()
    {
        var (context, _) = CrearContexto();
        await using var _ = context;

        var resultado = await ControladorUsuarios(context).CreateUsuario(new CreateUsuarioDto
        {
            Nombre = "Nuevo",
            Correo = "nuevo@ecar.test",
            Password = "123"
        });

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task ConPasswordPendiente_SoloSePermiteCambiarla()
    {
        var filtro = new CambioPasswordPendienteFilter();
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AuthService.ClaimCambioPasswordPendiente, "true")], "Pruebas"));

        var bloqueada = await EjecutarFiltroAsync(filtro, usuario, permitida: false);
        var permitida = await EjecutarFiltroAsync(filtro, usuario, permitida: true);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(bloqueada).StatusCode);
        Assert.Null(permitida);
    }

    private static async Task<IActionResult?> EjecutarFiltroAsync(
        CambioPasswordPendienteFilter filtro, ClaimsPrincipal usuario, bool permitida)
    {
        var descriptor = new ActionDescriptor
        {
            EndpointMetadata = permitida ? [new PermitirCambioPasswordPendienteAttribute()] : []
        };
        var actionContext = new ActionContext(new DefaultHttpContext { User = usuario }, new RouteData(), descriptor);
        var contexto = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());

        await filtro.OnActionExecutionAsync(contexto,
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], new object())));
        return contexto.Result;
    }
}
