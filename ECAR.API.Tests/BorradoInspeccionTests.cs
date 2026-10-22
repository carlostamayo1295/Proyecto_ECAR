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
/// Fase 4 (PLAN_FASE4_TAREAS §3.6): una inspección en curso ya no se borra, se anula con motivo y
/// conserva sus fotos; y el alta manual rechaza un equipo desactivado (la misma regla que el QR).
/// </summary>
public class BorradoInspeccionTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Operador";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public List<string> Eliminados { get; } = new();
        public string? FallaAlBorrar { get; init; }

        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"2026/10/{idInspeccion}/evidencia{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa)
        {
            if (rutaRelativa == FallaAlBorrar)
            {
                throw new IOException("El archivo está en uso");
            }

            Eliminados.Add(rutaRelativa);
            return Task.CompletedTask;
        }
    }

    private sealed record Escenario(ECARDbContext Context, Usuario Tecnico, Equipo Equipo, Checklist Checklist);

    private static async Task<Escenario> SeedAsync(bool equipoActivo = true)
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var tecnico = new Usuario { Nombre = "Operador", Correo = "tecnico@ecar.test", PasswordHash = FirmaPruebas.PasswordHash, Activo = true };
        var equipo = new Equipo
        {
            CodigoInterno = "EQ-001",
            ActivoFijo = "AF-001",
            NombreEquipo = "Balanza",
            Activo = equipoActivo
        };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true };

        context.AddRange(tecnico, equipo, checklist);
        await context.SaveChangesAsync();
        return new Escenario(context, tecnico, equipo, checklist);
    }

    private static async Task<Inspeccion> AgregarInspeccionAsync(
        Escenario escenario, string estado, params string[] fotos)
    {
        var inspeccion = new Inspeccion
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdUsuario = escenario.Tecnico.IdUsuario,
            IdChecklist = escenario.Checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = estado
        };
        escenario.Context.Inspecciones.Add(inspeccion);
        await escenario.Context.SaveChangesAsync();

        foreach (var foto in fotos)
        {
            escenario.Context.Evidencias.Add(new Evidencia
            {
                IdInspeccion = inspeccion.IdInspeccion,
                Archivo = foto,
                NombreOriginal = "foto.jpg",
                TipoContenido = "image/jpeg",
                TamanoBytes = 4,
                FechaCarga = DateTime.UtcNow,
                IdUsuarioCarga = escenario.Tecnico.IdUsuario
            });
        }

        await escenario.Context.SaveChangesAsync();
        escenario.Context.ChangeTracker.Clear();
        return inspeccion;
    }

    private static InspeccionesController Controlador(Escenario escenario, IEvidenciaStorage storage) =>
        Fabrica.Inspecciones(escenario.Context, new FakeCurrentUser(escenario.Tecnico.IdUsuario, "Operador"), storage);

    private static readonly MotivoDto Motivo = new() { Motivo = "Se abrió sobre el equipo equivocado" };

    // Antes DELETE borraba la inspección en curso y sus fotos. Ahora ninguna inspección se borra
    // físicamente: se anula con POST {id}/anular, y DELETE responde 409 sin tocar nada.

    [Fact]
    public async Task AnularInspeccion_EnCurso_LaConservaConSusFotosYElMotivo()
    {
        var escenario = await SeedAsync();
        var inspeccion = await AgregarInspeccionAsync(escenario, InspeccionEstados.EnCurso,
            "2026/10/1/a.jpg", "2026/10/1/b.jpg");
        var storage = new FakeStorage();

        var action = await Controlador(escenario, storage).AnularInspeccion(inspeccion.IdInspeccion, Motivo);

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Empty(storage.Eliminados); // §3.6: nada se borra
        var anulada = await escenario.Context.Inspecciones.AsNoTracking()
            .Include(i => i.Evidencias)
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.Anulada, anulada.Estado);
        Assert.Equal(Motivo.Motivo, anulada.MotivoAnulacion);
        Assert.Equal(escenario.Tecnico.IdUsuario, anulada.IdUsuarioAnulacion);
        Assert.NotNull(anulada.FechaAnulacion);
        Assert.Equal(2, anulada.Evidencias.Count);
    }

    [Fact]
    public async Task AnularInspeccion_Cerrada_Devuelve409YNoTocaNada()
    {
        var escenario = await SeedAsync();
        var inspeccion = await AgregarInspeccionAsync(escenario, InspeccionEstados.Cerrada, "2026/10/1/a.jpg");
        var storage = new FakeStorage();

        var action = await Controlador(escenario, storage).AnularInspeccion(inspeccion.IdInspeccion, Motivo);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InspeccionEstados.Cerrada, (await escenario.Context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion)).Estado);
    }

    [Fact]
    public async Task UnaInspeccionAnulada_NoAdmiteRespuestasNiFirma()
    {
        var escenario = await SeedAsync();
        var inspeccion = await AgregarInspeccionAsync(escenario, InspeccionEstados.Anulada);
        var controlador = Controlador(escenario, new FakeStorage());

        var respuestas = await controlador.GuardarRespuestas(inspeccion.IdInspeccion, new GuardarRespuestasDto());
        var firma = await controlador.FirmarConPassword(escenario.Context, inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            Password = Fabrica.ClaveFirma,
            FirmaPngBase64 = "iVBORw0KGgo="
        });

        Assert.IsType<ConflictObjectResult>(respuestas.Result);
        Assert.IsType<ConflictObjectResult>(firma.Result);
    }

    [Fact]
    public void Inspecciones_NoTieneDelete()
    {
        // Sin acción DELETE, ASP.NET responde 405 en api/inspecciones/{id} (PLAN_FASE4_TAREAS §3.6).
        Assert.DoesNotContain(typeof(InspeccionesController).GetMethods(), m =>
            m.GetCustomAttributes(typeof(HttpDeleteAttribute), true).Length > 0);
    }

    [Fact]
    public async Task CreateInspeccion_ConEquipoDesactivado_Devuelve400()
    {
        var escenario = await SeedAsync(equipoActivo: false);

        var action = await Controlador(escenario, new FakeStorage()).CreateInspeccion(new CreateInspeccionDto
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist,
            FechaInspeccion = DateTime.Today
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var respuesta = Assert.IsType<ApiResponse<InspeccionDto>>(badRequest.Value);
        Assert.Equal("El equipo indicado no existe o no está activo", respuesta.Message);
        Assert.False(await escenario.Context.Inspecciones.AnyAsync());
    }
}
