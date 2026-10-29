using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// B-04 (ECAR-221): GET /api/reportes/usuarios, la búsqueda del filtro «Usuario» de los reportes.
/// La usan el Administrador y el Supervisor, y devuelve solo id, nombre y correo.
/// </summary>
public class BusquedaUsuariosReportesTests
{
    private sealed class Supervisor : ICurrentUser
    {
        public long IdUsuario => 2;
        public string Nombre => "Sara Supervisora";
        public IReadOnlyCollection<string> Roles => [RolesSistema.Supervisor];
        public bool IsInRole(string role) => role == RolesSistema.Supervisor;
    }

    private static async Task<ECARDbContext> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var operador = new Rol { Nombre = RolesSistema.Operador };
        db.Usuarios.AddRange(
            new Usuario { Nombre = "Ana Pérez", Correo = "ana@ecar.test", PasswordHash = "hash-secreto", UsuarioAD = "ECAR\\ana",
                UsuarioRoles = [new UsuarioRol { Rol = operador }] },
            new Usuario { Nombre = "Bruno Díaz", Correo = "bdiaz@ecar.test", PasswordHash = "hash-secreto" },
            new Usuario { Nombre = "Carla Ruiz", Correo = "carla.ana@ecar.test", PasswordHash = "hash-secreto", Activo = false });
        await db.SaveChangesAsync();
        return db;
    }

    private static ReportesController Controlador(ECARDbContext db) => new(new ReporteService(db), db, new Supervisor());

    private static List<UsuarioBusquedaDto> Datos(ActionResult<ApiResponse<List<UsuarioBusquedaDto>>> resultado) =>
        Assert.IsType<ApiResponse<List<UsuarioBusquedaDto>>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;

    [Fact]
    public async Task Supervisor_recibe_200_con_los_usuarios_que_coinciden_por_nombre_o_correo()
    {
        await using var db = await SeedAsync();

        var usuarios = Datos(await Controlador(db).GetUsuarios("  ana "));

        // «Ana Pérez» por el nombre y «Carla Ruiz» (inactiva) por el correo, en orden de nombre.
        Assert.Equal(["Ana Pérez", "Carla Ruiz"], usuarios.Select(u => u.Nombre));
        Assert.Equal("ana@ecar.test", usuarios[0].Correo);
    }

    [Fact]
    public async Task Sin_texto_devuelve_como_maximo_20_usuarios()
    {
        await using var db = await SeedAsync();
        db.Usuarios.AddRange(Enumerable.Range(1, 30).Select(i =>
            new Usuario { Nombre = $"Usuario {i:00}", Correo = $"u{i}@ecar.test", PasswordHash = "hash-secreto" }));
        await db.SaveChangesAsync();

        var usuarios = Datos(await Controlador(db).GetUsuarios());

        Assert.Equal(ReportesController.MaximoUsuariosBusqueda, usuarios.Count);
    }

    [Fact]
    public async Task La_respuesta_no_incluye_contrasenas_ni_roles()
    {
        await using var db = await SeedAsync();

        var json = JsonSerializer.Serialize(Datos(await Controlador(db).GetUsuarios("ana")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(["Correo", "IdUsuario", "Nombre"],
            typeof(UsuarioBusquedaDto).GetProperties().Select(p => p.Name).Order());
        foreach (var prohibido in new[] { "password", "hash-secreto", "roles", "operador", "usuarioAD", "securityStamp" })
        {
            Assert.DoesNotContain(prohibido, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(RolesSistema.Supervisor, true)]
    [InlineData(RolesSistema.Administrador, true)]
    [InlineData(RolesSistema.Operador, false)]
    [InlineData(RolesSistema.Estandar, false)]
    public async Task Solo_Administrador_y_Supervisor_pueden_buscar(string rol, bool permitido)
    {
        var metodo = typeof(ReportesController).GetMethod(nameof(ReportesController.GetUsuarios))!;
        Assert.Null(metodo.GetCustomAttribute<AllowAnonymousAttribute>());

        // La política efectiva es la combinación de los [Authorize] de la clase y de la acción, como en el
        // pipeline: si no se cumple, el API responde 403 a un usuario autenticado.
        var servicios = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        var atributos = typeof(ReportesController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(metodo.GetCustomAttributes<AuthorizeAttribute>());
        var politica = await AuthorizationPolicy.CombineAsync(
            servicios.GetRequiredService<IAuthorizationPolicyProvider>(), atributos);
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol)], "Pruebas"));

        var resultado = await servicios.GetRequiredService<IAuthorizationService>().AuthorizeAsync(usuario, politica!);

        Assert.Equal(permitido, resultado.Succeeded);
    }
}
