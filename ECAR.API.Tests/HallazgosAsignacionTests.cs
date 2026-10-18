using System.Text.Json;
using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 5 de hallazgos: PUT api/hallazgos/{id} asigna el responsable y la fecha de compromiso.
/// El responsable debe ser un usuario activo, la fecha no puede ser pasada y un hallazgo
/// cerrado o anulado no se reasigna.
/// </summary>
public class HallazgosAsignacionTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Administrador";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Escenario(ECARDbContext Context, Usuario Admin, Usuario Ana, Usuario Luis, Usuario Inactivo,
        Hallazgo Hallazgo);

    private static async Task<Escenario> SeedAsync(string estado = HallazgoEstados.Abierto)
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var admin = new Usuario { Nombre = "Admin", Correo = "admin@ecar.test", PasswordHash = "hash", Activo = true };
        var ana = new Usuario { Nombre = "Ana", Correo = "ana@ecar.test", PasswordHash = "hash", Activo = true };
        var luis = new Usuario { Nombre = "Luis", Correo = "luis@ecar.test", PasswordHash = "hash", Activo = true };
        var inactivo = new Usuario { Nombre = "Inactivo", Correo = "inactivo@ecar.test", PasswordHash = "hash", Activo = false };
        // El responsable debe ser un usuario activo con rol Técnico o Administrador.
        var rolTecnico = new Rol { Nombre = "Técnico" };
        admin.UsuarioRoles.Add(new UsuarioRol { Rol = new Rol { Nombre = "Administrador" } });
        ana.UsuarioRoles.Add(new UsuarioRol { Rol = rolTecnico });
        luis.UsuarioRoles.Add(new UsuarioRol { Rol = rolTecnico });
        inactivo.UsuarioRoles.Add(new UsuarioRol { Rol = rolTecnico });
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(admin, ana, luis, inactivo, equipo, checklist);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = ana.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.Cerrada
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var hallazgo = new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = "Fuga de aceite",
            Estado = estado,
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = ana.IdUsuario
        };
        context.Hallazgos.Add(hallazgo);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, admin, ana, luis, inactivo, hallazgo);
    }

    private static Task<ActionResult<ApiResponse<HallazgoDto>>> AsignarAsync(Escenario e, UpdateHallazgoDto dto, long? id = null) =>
        new HallazgosController(e.Context, new FakeCurrentUser(e.Admin.IdUsuario, "Administrador"), new MotivoCambio())
            .UpdateHallazgo(id ?? e.Hallazgo.IdHallazgo, dto);

    private static Task<Hallazgo> GuardadoAsync(Escenario e) =>
        e.Context.Hallazgos.AsNoTracking().SingleAsync(h => h.IdHallazgo == e.Hallazgo.IdHallazgo);

    private static string Mensaje(IActionResult? resultado) =>
        Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsAssignableFrom<ObjectResult>(resultado).Value).Message;

    [Fact]
    public async Task Asignar_ResponsableYFecha_LosGuardaYDevuelveElNombre()
    {
        var e = await SeedAsync();
        var fecha = DateTime.Today.AddDays(7);

        var action = await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario, FechaCompromiso = fecha });

        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(e.Luis.IdUsuario, dto.IdUsuarioResponsable);
        Assert.Equal("Luis", dto.NombreResponsable);
        Assert.Equal(fecha, dto.FechaCompromiso);

        var guardado = await GuardadoAsync(e);
        Assert.Equal(e.Luis.IdUsuario, guardado.IdUsuarioResponsable);
        Assert.Equal(fecha, guardado.FechaCompromiso);
        Assert.Equal(HallazgoEstados.Abierto, guardado.Estado);
    }

    [Fact]
    public async Task Reasignar_CambiaElResponsableYElNombreDevuelto()
    {
        var e = await SeedAsync();
        await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario });
        e.Context.ChangeTracker.Clear();

        var action = await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Ana.IdUsuario });

        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal("Ana", dto.NombreResponsable);
        Assert.Equal(e.Ana.IdUsuario, (await GuardadoAsync(e)).IdUsuarioResponsable);
    }

    [Fact]
    public async Task Asignar_SoloUnoDeLosDos_ConservaElOtro()
    {
        var e = await SeedAsync();
        var fecha = DateTime.Today.AddDays(3);
        await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario, FechaCompromiso = fecha });
        e.Context.ChangeTracker.Clear();

        await AsignarAsync(e, new UpdateHallazgoDto { FechaCompromiso = fecha.AddDays(1) });
        e.Context.ChangeTracker.Clear();
        var trasFecha = await GuardadoAsync(e);
        await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Ana.IdUsuario });
        var trasResponsable = await GuardadoAsync(e);

        Assert.Equal(e.Luis.IdUsuario, trasFecha.IdUsuarioResponsable);
        Assert.Equal(fecha.AddDays(1), trasFecha.FechaCompromiso);
        Assert.Equal(e.Ana.IdUsuario, trasResponsable.IdUsuarioResponsable);
        Assert.Equal(fecha.AddDays(1), trasResponsable.FechaCompromiso);
    }

    [Fact]
    public async Task Asignar_ConFechaDeHoy_SePermite()
    {
        var e = await SeedAsync();

        var action = await AsignarAsync(e, new UpdateHallazgoDto { FechaCompromiso = DateTime.Today });

        Assert.IsType<OkObjectResult>(action.Result);
    }

    [Fact]
    public async Task Asignar_ConFechaPasada_Devuelve400YNoCambiaNada()
    {
        var e = await SeedAsync();

        var action = await AsignarAsync(e, new UpdateHallazgoDto
        {
            Descripcion = "Otra descripción",
            IdUsuarioResponsable = e.Luis.IdUsuario,
            FechaCompromiso = DateTime.Today.AddDays(-1)
        });

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeFechaCompromisoPasada, Mensaje(action.Result));
        var guardado = await GuardadoAsync(e);
        Assert.Null(guardado.IdUsuarioResponsable);
        Assert.Null(guardado.FechaCompromiso);
        Assert.Equal("Fuga de aceite", guardado.Descripcion);
    }

    [Fact]
    public async Task Asignar_AUnResponsableInactivoOInexistente_Devuelve400()
    {
        var e = await SeedAsync();

        foreach (var idResponsable in new[] { e.Inactivo.IdUsuario, 999L })
        {
            var action = await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = idResponsable });

            Assert.IsType<BadRequestObjectResult>(action.Result);
            Assert.Equal(HallazgosController.MensajeResponsableInvalido, Mensaje(action.Result));
        }

        Assert.Null((await GuardadoAsync(e)).IdUsuarioResponsable);
    }

    [Fact]
    public async Task Asignar_UnHallazgoEnProceso_SePermite()
    {
        var e = await SeedAsync(HallazgoEstados.EnProceso);

        var action = await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario });

        Assert.IsType<OkObjectResult>(action.Result);
    }

    [Theory]
    [InlineData(HallazgoEstados.Cerrado)]
    [InlineData(HallazgoEstados.Anulado)]
    public async Task Asignar_UnHallazgoCerradoOAnulado_Devuelve409(string estado)
    {
        var e = await SeedAsync(estado);

        var action = await AsignarAsync(e, new UpdateHallazgoDto
        {
            IdUsuarioResponsable = e.Luis.IdUsuario,
            FechaCompromiso = DateTime.Today.AddDays(1)
        });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeEstadoFinal, Mensaje(action.Result));
        var guardado = await GuardadoAsync(e);
        Assert.Null(guardado.IdUsuarioResponsable);
        Assert.Null(guardado.FechaCompromiso);
    }

    [Fact]
    public async Task Asignar_UnHallazgoInexistente_Devuelve404()
    {
        var e = await SeedAsync();

        var action = await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario }, id: 999);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task Asignar_QuedaEnLaAuditoriaConElValorAnteriorYElNuevo()
    {
        var e = await SeedAsync();

        await AsignarAsync(e, new UpdateHallazgoDto { IdUsuarioResponsable = e.Luis.IdUsuario });

        var fila = await e.Context.Auditoria.AsNoTracking()
            .Where(a => a.Tabla == "Hallazgos" && a.RegistroId == e.Hallazgo.IdHallazgo && a.Accion == AuditoriaAcciones.Modificar)
            .SingleAsync();
        var nuevo = JsonDocument.Parse(fila.ValorNuevo!).RootElement;
        var anterior = JsonDocument.Parse(fila.ValorAnterior!).RootElement;
        Assert.Equal(e.Luis.IdUsuario, nuevo.GetProperty(nameof(Hallazgo.IdUsuarioResponsable)).GetInt64());
        Assert.Equal(JsonValueKind.Null, anterior.GetProperty(nameof(Hallazgo.IdUsuarioResponsable)).ValueKind);
    }
}
