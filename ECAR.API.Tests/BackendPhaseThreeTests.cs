using System.Security.Claims;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

public class BackendPhaseThreeTests
{
    private sealed class FakeCurrentUser(
        long idUsuario,
        string nombre,
        params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => nombre;
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ECARDbContext(options);
    }

    private static async Task<(Usuario Tecnico, Usuario OtroTecnico, Equipo Equipo, Checklist Checklist)>
        SeedEscenarioAsync(ECARDbContext context)
    {
        var tecnico = new Usuario
        {
            Nombre = "Técnico Uno",
            Correo = "tecnico1@ecar.test",
            PasswordHash = "hash",
            Activo = true
        };
        var otroTecnico = new Usuario
        {
            Nombre = "Técnico Dos",
            Correo = "tecnico2@ecar.test",
            PasswordHash = "hash",
            Activo = true
        };
        var equipo = new Equipo
        {
            CodigoInterno = "EQ-001",
            ActivoFijo = "AF-001",
            NombreEquipo = "Bomba de prueba",
            Activo = true
        };
        var checklist = new Checklist
        {
            Nombre = "Checklist de bomba",
            Version = "1.0",
            Activo = true,
            Preguntas =
            [
                new PreguntaChecklist
                {
                    Pregunta = "Pregunta segunda",
                    TipoRespuesta = "Texto",
                    Orden = 2
                },
                new PreguntaChecklist
                {
                    Pregunta = "Pregunta primera",
                    TipoRespuesta = "SiNo",
                    Obligatoria = true,
                    Orden = 1
                }
            ]
        };

        context.AddRange(tecnico, otroTecnico, equipo, checklist);
        await context.SaveChangesAsync();
        return (tecnico, otroTecnico, equipo, checklist);
    }

    [Fact]
    public void CurrentUser_LeeIdentidadYRolesDesdeElToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "Carlos Tamayo"),
            new Claim(ClaimTypes.Role, "Administrador"),
            new Claim(ClaimTypes.Role, "Técnico")
        ], "Test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        ICurrentUser currentUser = new CurrentUser(accessor);

        Assert.Equal(42, currentUser.IdUsuario);
        Assert.Equal("Carlos Tamayo", currentUser.Nombre);
        Assert.Equal(["Administrador", "Técnico"], currentUser.Roles);
        Assert.True(currentUser.IsInRole("Administrador"));
    }

    [Fact]
    public void CurrentUser_RechazaTokenSinIdentificadorValido()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "usuario-invalido")
        ], "Test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        ICurrentUser currentUser = new CurrentUser(accessor);

        var exception = Assert.Throws<InvalidOperationException>(() => currentUser.IdUsuario);

        Assert.Contains("identificador de usuario válido", exception.Message);
    }

    [Fact]
    public async Task IniciarInspeccion_UsaUsuarioDelTokenYPreguntasOrdenadas()
    {
        await using var context = CreateContext();
        var escenario = await SeedEscenarioAsync(context);
        var currentUser = new FakeCurrentUser(
            escenario.Tecnico.IdUsuario,
            escenario.Tecnico.Nombre,
            "Técnico");
        var controller = new InspeccionesController(context, currentUser);

        var action = await controller.IniciarInspeccion(new IniciarInspeccionDto
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist
        });

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(created.Value);
        Assert.True(response.Success);
        Assert.Equal(escenario.Tecnico.IdUsuario, response.Data!.IdUsuario);
        Assert.Equal(InspeccionEstados.EnCurso, response.Data.Estado);
        Assert.Equal([1, 2], response.Data.Preguntas.Select(p => p.Orden).ToArray());

        var guardada = Assert.Single(await context.Inspecciones.ToListAsync());
        Assert.Equal(escenario.Tecnico.IdUsuario, guardada.IdUsuario);
        Assert.Equal(escenario.Checklist.IdChecklist, guardada.IdChecklist);
    }

    [Fact]
    public async Task IniciarInspeccion_Devuelve409YLaExistenteSiYaEstaEnCurso()
    {
        await using var context = CreateContext();
        var escenario = await SeedEscenarioAsync(context);
        var controller = new InspeccionesController(
            context,
            new FakeCurrentUser(escenario.Tecnico.IdUsuario, escenario.Tecnico.Nombre, "Técnico"));
        var dto = new IniciarInspeccionDto
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist
        };

        var primera = await controller.IniciarInspeccion(dto);
        var primeraCreada = Assert.IsType<CreatedAtActionResult>(primera.Result);
        var primeraRespuesta = Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(primeraCreada.Value);

        var segunda = await controller.IniciarInspeccion(dto);

        var conflicto = Assert.IsType<ConflictObjectResult>(segunda.Result);
        var conflictoRespuesta = Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(conflicto.Value);
        Assert.Equal(primeraRespuesta.Data!.IdInspeccion, conflictoRespuesta.Data!.IdInspeccion);
        Assert.Single(await context.Inspecciones.ToListAsync());
    }

    [Fact]
    public async Task GetEjecucion_IncluyeRespuestaActualEnSuPregunta()
    {
        await using var context = CreateContext();
        var escenario = await SeedEscenarioAsync(context);
        var pregunta = await context.PreguntasChecklist
            .SingleAsync(p => p.Orden == 1);
        var inspeccion = new Inspeccion
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist,
            IdUsuario = escenario.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();
        context.RespuestasInspeccion.Add(new RespuestaInspeccion
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "Si",
            Observacion = "Equipo operativo"
        });
        await context.SaveChangesAsync();
        var controller = new InspeccionesController(
            context,
            new FakeCurrentUser(escenario.Tecnico.IdUsuario, escenario.Tecnico.Nombre, "Técnico"));

        var action = await controller.GetEjecucion(inspeccion.IdInspeccion);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(ok.Value);
        var primeraPregunta = Assert.Single(response.Data!.Preguntas, p => p.Orden == 1);
        Assert.Equal("Si", primeraPregunta.Respuesta);
        Assert.Equal("Equipo operativo", primeraPregunta.Observacion);
    }

    [Fact]
    public async Task GetEjecucion_RechazaTecnicoSobreInspeccionAjena()
    {
        await using var context = CreateContext();
        var escenario = await SeedEscenarioAsync(context);
        var inspeccion = new Inspeccion
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist,
            IdUsuario = escenario.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();
        var controller = new InspeccionesController(
            context,
            new FakeCurrentUser(escenario.OtroTecnico.IdUsuario, escenario.OtroTecnico.Nombre, "Técnico"));

        var action = await controller.GetEjecucion(inspeccion.IdInspeccion);

        Assert.IsType<ForbidResult>(action.Result);
    }
}
