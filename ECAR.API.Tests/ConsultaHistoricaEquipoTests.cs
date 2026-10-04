using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Consulta histórica de un equipo (SRS §2 y §6): GET /api/inspecciones?idEquipo= devuelve solo
/// las inspecciones de ese equipo y sigue aplicando la regla "propia" del Técnico.
/// </summary>
public class ConsultaHistoricaEquipoTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Usuario";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"2026/10/{idInspeccion}/evidencia{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private sealed record Escenario(ECARDbContext Context, Usuario TecnicoA, Usuario TecnicoB, Equipo Balanza, Equipo Bomba);

    private static async Task<Escenario> SeedAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tecnicoA = new Usuario { Nombre = "Técnico A", Correo = "a@ecar.test", PasswordHash = "hash", Activo = true };
        var tecnicoB = new Usuario { Nombre = "Técnico B", Correo = "b@ecar.test", PasswordHash = "hash", Activo = true };
        var balanza = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var bomba = new Equipo { CodigoInterno = "EQ-002", ActivoFijo = "AF-002", NombreEquipo = "Bomba", Activo = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };
        context.AddRange(tecnicoA, tecnicoB, balanza, bomba, checklist);
        await context.SaveChangesAsync();

        // Balanza: una de A y una de B. Bomba: una de A.
        foreach (var (equipo, tecnico) in new[] { (balanza, tecnicoA), (balanza, tecnicoB), (bomba, tecnicoA) })
        {
            context.Inspecciones.Add(new Inspeccion
            {
                IdEquipo = equipo.IdEquipo,
                IdUsuario = tecnico.IdUsuario,
                IdChecklist = checklist.IdChecklist,
                FechaInspeccion = DateTime.UtcNow,
                Estado = InspeccionEstados.Cerrada
            });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Escenario(context, tecnicoA, tecnicoB, balanza, bomba);
    }

    private static async Task<List<InspeccionDto>> HistorialAsync(Escenario escenario, ICurrentUser usuario, long idEquipo)
    {
        var controlador = new InspeccionesController(escenario.Context, usuario, new InspeccionService(escenario.Context),
            new FakeStorage(), NullLogger<InspeccionesController>.Instance);

        var action = await controlador.GetInspecciones(idEquipo: idEquipo);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        return Assert.IsType<ApiResponse<PagedResultDto<InspeccionDto>>>(ok.Value).Data!.Data;
    }

    [Fact]
    public async Task Auditor_VeTodasLasInspeccionesDelEquipoYSoloLasDeEse()
    {
        var escenario = await SeedAsync();

        var historial = await HistorialAsync(escenario, new FakeCurrentUser(999, "Auditor"), escenario.Balanza.IdEquipo);

        Assert.Equal(2, historial.Count);
        Assert.All(historial, i => Assert.Equal(escenario.Balanza.IdEquipo, i.IdEquipo));
    }

    [Fact]
    public async Task Tecnico_VeSoloSusInspeccionesDelEquipo()
    {
        var escenario = await SeedAsync();

        var historial = await HistorialAsync(escenario,
            new FakeCurrentUser(escenario.TecnicoB.IdUsuario, "Técnico"), escenario.Balanza.IdEquipo);

        var inspeccion = Assert.Single(historial);
        Assert.Equal(escenario.TecnicoB.IdUsuario, inspeccion.IdUsuario);
        Assert.Equal(escenario.Balanza.IdEquipo, inspeccion.IdEquipo);
    }
}
