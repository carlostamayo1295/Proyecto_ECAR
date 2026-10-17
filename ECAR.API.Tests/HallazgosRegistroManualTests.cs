using System.ComponentModel.DataAnnotations;
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
/// Fase 3 de hallazgos: POST api/hallazgos registra un hallazgo manual sobre una inspección
/// existente, siempre Abierto y a nombre de quien lo registra.
/// </summary>
public class HallazgosRegistroManualTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Técnico";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Escenario(ECARDbContext Context, Usuario Tecnico, Usuario Responsable, Usuario Inactivo,
        Inspeccion EnCurso, Inspeccion Cerrada, Inspeccion Anulada);

    private static async Task<Escenario> SeedAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tecnico = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.test", PasswordHash = "hash", Activo = true };
        var responsable = new Usuario { Nombre = "Responsable", Correo = "resp@ecar.test", PasswordHash = "hash", Activo = true };
        var inactivo = new Usuario { Nombre = "Inactivo", Correo = "inactivo@ecar.test", PasswordHash = "hash", Activo = false };
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Criticidad = "Alta", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(tecnico, responsable, inactivo, equipo, checklist);
        await context.SaveChangesAsync();

        Inspeccion Nueva(string estado) => new()
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = tecnico.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = estado
        };
        var enCurso = Nueva(InspeccionEstados.EnCurso);
        var cerrada = Nueva(InspeccionEstados.Cerrada);
        var anulada = Nueva(InspeccionEstados.Anulada);
        context.Inspecciones.AddRange(enCurso, cerrada, anulada);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, tecnico, responsable, inactivo, enCurso, cerrada, anulada);
    }

    private static HallazgosController Controlador(Escenario escenario, Usuario? usuario = null) =>
        new(escenario.Context, new FakeCurrentUser((usuario ?? escenario.Tecnico).IdUsuario, "Técnico"), new MotivoCambio());

    private static CreateHallazgoDto Alta(Inspeccion inspeccion) => new()
    {
        IdInspeccion = inspeccion.IdInspeccion,
        Descripcion = "  Fuga de aceite en la base  ",
        Criticidad = "Media"
    };

    private static string Mensaje(IActionResult? resultado) =>
        Assert.IsType<ApiResponse<HallazgoDto>>(Assert.IsAssignableFrom<ObjectResult>(resultado).Value).Message;

    [Fact]
    public async Task Crear_SobreUnaInspeccionEnCurso_LoRegistraAbiertoYManual()
    {
        var escenario = await SeedAsync();

        var action = await Controlador(escenario).CreateHallazgo(Alta(escenario.EnCurso));

        var creado = Assert.IsType<CreatedAtActionResult>(action.Result);
        var dto = Assert.IsType<ApiResponse<HallazgoDto>>(creado.Value).Data!;
        Assert.Equal(HallazgoEstados.Abierto, dto.Estado);
        Assert.Equal(HallazgoOrigenes.Manual, dto.Origen);
        Assert.Equal(escenario.EnCurso.IdEquipo, dto.IdEquipo);

        var guardado = await escenario.Context.Hallazgos.AsNoTracking().SingleAsync();
        Assert.Equal(escenario.EnCurso.IdInspeccion, guardado.IdInspeccion);
        Assert.Equal("Fuga de aceite en la base", guardado.Descripcion);
        Assert.Equal("Media", guardado.Criticidad);
        Assert.Equal(HallazgoEstados.Abierto, guardado.Estado);
        Assert.Equal(HallazgoOrigenes.Manual, guardado.Origen);
        Assert.Equal(escenario.Tecnico.IdUsuario, guardado.IdUsuarioRegistro);
        Assert.Null(guardado.IdPregunta);
        Assert.Null(guardado.IdRespuesta);
    }

    [Fact]
    public async Task Crear_SobreUnaInspeccionCerrada_TambienSePermite()
    {
        var escenario = await SeedAsync();

        var action = await Controlador(escenario).CreateHallazgo(Alta(escenario.Cerrada));

        Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.True(await escenario.Context.Hallazgos.AnyAsync(h => h.IdInspeccion == escenario.Cerrada.IdInspeccion));
    }

    [Fact]
    public async Task Crear_QuedaEnLaAuditoria()
    {
        var escenario = await SeedAsync();

        await Controlador(escenario).CreateHallazgo(Alta(escenario.EnCurso));

        var hallazgo = await escenario.Context.Hallazgos.AsNoTracking().SingleAsync();
        Assert.True(await escenario.Context.Auditoria.AnyAsync(a =>
            a.Tabla == "Hallazgos" && a.RegistroId == hallazgo.IdHallazgo && a.Accion == AuditoriaAcciones.Crear));
    }

    [Fact]
    public async Task Crear_ConInspeccionInexistente_Devuelve400()
    {
        var escenario = await SeedAsync();

        var action = await Controlador(escenario).CreateHallazgo(new CreateHallazgoDto { IdInspeccion = 999, Descripcion = "X" });

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("La inspección indicada no existe", Mensaje(action.Result));
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Crear_SobreUnaInspeccionAnulada_Devuelve409()
    {
        var escenario = await SeedAsync();

        var action = await Controlador(escenario).CreateHallazgo(Alta(escenario.Anulada));

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeInspeccionAnulada, Mensaje(action.Result));
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Crear_SinDescripcion_Devuelve400(string descripcion)
    {
        var escenario = await SeedAsync();
        var dto = new CreateHallazgoDto { IdInspeccion = escenario.EnCurso.IdInspeccion, Descripcion = descripcion };

        // La validación del modelo (la que aplica [ApiController]) ya lo rechaza...
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), [], validateAllProperties: true));

        // ...y el controlador también, por si se llama sin ella.
        var action = await Controlador(escenario).CreateHallazgo(dto);
        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public void Crear_ConCriticidadDeMasDe20Caracteres_NoPasaLaValidacionDelModelo()
    {
        var dto = new CreateHallazgoDto { IdInspeccion = 1, Descripcion = "X", Criticidad = new string('A', 21) };

        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), [], validateAllProperties: true));
    }

    [Fact]
    public async Task Crear_ConResponsableYFechaDeCompromisoValidos_LosGuarda()
    {
        var escenario = await SeedAsync();
        var dto = Alta(escenario.EnCurso);
        dto.IdUsuarioResponsable = escenario.Responsable.IdUsuario;
        dto.FechaCompromiso = DateTime.Today;

        var action = await Controlador(escenario).CreateHallazgo(dto);

        Assert.IsType<CreatedAtActionResult>(action.Result);
        var guardado = await escenario.Context.Hallazgos.AsNoTracking().SingleAsync();
        Assert.Equal(escenario.Responsable.IdUsuario, guardado.IdUsuarioResponsable);
        Assert.Equal(DateTime.Today, guardado.FechaCompromiso);
    }

    [Fact]
    public async Task Crear_ConResponsableInactivoOInexistente_Devuelve400()
    {
        var escenario = await SeedAsync();

        foreach (var idResponsable in new[] { escenario.Inactivo.IdUsuario, 999L })
        {
            var dto = Alta(escenario.EnCurso);
            dto.IdUsuarioResponsable = idResponsable;

            var action = await Controlador(escenario).CreateHallazgo(dto);

            Assert.IsType<BadRequestObjectResult>(action.Result);
            Assert.Equal(HallazgosController.MensajeResponsableInvalido, Mensaje(action.Result));
        }

        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Crear_ConFechaDeCompromisoPasada_Devuelve400()
    {
        var escenario = await SeedAsync();
        var dto = Alta(escenario.EnCurso);
        dto.FechaCompromiso = DateTime.Today.AddDays(-1);

        var action = await Controlador(escenario).CreateHallazgo(dto);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(HallazgosController.MensajeFechaCompromisoPasada, Mensaje(action.Result));
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Crear_ConUnUsuarioAutenticadoInactivo_Devuelve400()
    {
        var escenario = await SeedAsync();

        var action = await Controlador(escenario, escenario.Inactivo).CreateHallazgo(Alta(escenario.EnCurso));

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }
}
