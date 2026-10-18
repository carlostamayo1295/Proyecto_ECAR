using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 2 de hallazgos: firmar una inspección crea un hallazgo por cada respuesta "No", en el
/// mismo guardado que la firma, sin duplicarlos. La atomicidad real contra SQL Server está en
/// ECAR.IntegrationTests (HallazgosSqlServerTests): el proveedor en memoria no tiene transacciones.
/// </summary>
public class HallazgosFirmaTests
{
    private const string FirmaPngValida =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Técnico";
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

    /// <summary>Ejecuta <see cref="AlGuardarHallazgos"/> justo antes de guardar hallazgos nuevos.</summary>
    private sealed class InterceptorDeHallazgos : SaveChangesInterceptor
    {
        public Action? AlGuardarHallazgos { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Hallazgo>().Any(e => e.State == EntityState.Added))
            {
                AlGuardarHallazgos?.Invoke();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record Escenario(
        ECARDbContext Context,
        InMemoryDatabaseRoot Raiz,
        string NombreBase,
        InterceptorDeHallazgos Interceptor,
        Usuario Tecnico,
        PreguntaChecklist Limpieza,
        PreguntaChecklist Cables,
        PreguntaChecklist Etiqueta,
        PreguntaChecklist Lectura);

    private static ECARDbContext NuevoContexto(InMemoryDatabaseRoot raiz, string nombre,
        IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(nombre, raiz);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new ECARDbContext(builder.Options);
    }

    /// <summary>Checklist con tres preguntas Sí/No y una de texto, sobre un equipo de criticidad Alta.</summary>
    private static async Task<Escenario> SeedAsync()
    {
        var raiz = new InMemoryDatabaseRoot();
        var nombre = Guid.NewGuid().ToString();
        var interceptor = new InterceptorDeHallazgos();
        var context = NuevoContexto(raiz, nombre, interceptor);

        var tecnico = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.test", PasswordHash = FirmaPruebas.PasswordHash, Activo = true };
        var limpieza = new PreguntaChecklist { Pregunta = "¿Está limpio?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 1 };
        var cables = new PreguntaChecklist { Pregunta = "¿Cables en buen estado?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true, Orden = 2 };
        var etiqueta = new PreguntaChecklist { Pregunta = "¿Tiene etiqueta?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = false, Orden = 3 };
        var lectura = new PreguntaChecklist { Pregunta = "Lectura", TipoRespuesta = TiposRespuesta.Texto, Obligatoria = false, Orden = 4 };
        var checklist = new Checklist
        {
            Nombre = "Diario",
            Version = "1.0",
            Activo = true,
            Preguntas = [limpieza, cables, etiqueta, lectura]
        };
        var equipo = new Equipo
        {
            CodigoInterno = "EQ-001",
            ActivoFijo = "AF-001",
            NombreEquipo = "Balanza",
            Criticidad = "Alta",
            Activo = true
        };

        context.AddRange(tecnico, equipo, checklist);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Escenario(context, raiz, nombre, interceptor, tecnico, limpieza, cables, etiqueta, lectura);
    }

    private static async Task<Inspeccion> CrearInspeccionAsync(Escenario escenario, params RespuestaInspeccion[] respuestas)
    {
        var equipo = await escenario.Context.Equipos.SingleAsync();
        var checklist = await escenario.Context.Checklists.SingleAsync();
        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdChecklist = checklist.IdChecklist,
            IdUsuario = escenario.Tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        escenario.Context.Inspecciones.Add(inspeccion);
        await escenario.Context.SaveChangesAsync();

        foreach (var respuesta in respuestas)
        {
            respuesta.IdInspeccion = inspeccion.IdInspeccion;
            escenario.Context.RespuestasInspeccion.Add(respuesta);
        }

        await escenario.Context.SaveChangesAsync();
        escenario.Context.ChangeTracker.Clear();
        return inspeccion;
    }

    private static RespuestaInspeccion Si(PreguntaChecklist pregunta) =>
        new() { IdPregunta = pregunta.IdPregunta, Respuesta = "Si" };

    private static RespuestaInspeccion No(PreguntaChecklist pregunta, string observacion) =>
        new() { IdPregunta = pregunta.IdPregunta, Respuesta = "No", Observacion = observacion };

    private static Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarAsync(
        Escenario escenario, long idInspeccion) =>
        new InspeccionesController(escenario.Context, new FakeCurrentUser(escenario.Tecnico.IdUsuario, "Técnico"),
                new InspeccionService(escenario.Context), new FakeStorage(), NullLogger<InspeccionesController>.Instance)
            .FirmarConPassword(escenario.Context, idInspeccion, new FirmarInspeccionDto { FirmaPngBase64 = FirmaPngValida });

    [Fact]
    public async Task Firmar_CreaUnHallazgoAbiertoPorCadaRespuestaNo()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario,
            No(escenario.Limpieza, "Polvo acumulado"),
            No(escenario.Cables, " Cable pelado "),
            Si(escenario.Etiqueta));

        var action = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<OkObjectResult>(action.Result);
        var hallazgos = await escenario.Context.Hallazgos.AsNoTracking()
            .Where(h => h.IdInspeccion == inspeccion.IdInspeccion)
            .OrderBy(h => h.IdPregunta)
            .ToListAsync();
        var respuestas = await escenario.Context.RespuestasInspeccion.AsNoTracking()
            .Where(r => r.IdInspeccion == inspeccion.IdInspeccion)
            .ToDictionaryAsync(r => r.IdPregunta, r => r.IdRespuesta);
        var cerrada = await escenario.Context.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);

        Assert.Equal([escenario.Limpieza.IdPregunta, escenario.Cables.IdPregunta], hallazgos.Select(h => h.IdPregunta!.Value));
        Assert.Equal(["¿Está limpio?: Polvo acumulado", "¿Cables en buen estado?: Cable pelado"], hallazgos.Select(h => h.Descripcion));
        Assert.All(hallazgos, h =>
        {
            Assert.Equal(HallazgoEstados.Abierto, h.Estado);
            Assert.Equal(HallazgoOrigenes.Novedad, h.Origen);
            Assert.Equal("Alta", h.Criticidad);
            Assert.Equal(escenario.Tecnico.IdUsuario, h.IdUsuarioRegistro);
            Assert.Equal(respuestas[h.IdPregunta!.Value], h.IdRespuesta);
            Assert.Equal(cerrada.FechaCierre, h.FechaRegistro);
            Assert.Null(h.IdUsuarioResponsable);
        });
    }

    [Fact]
    public async Task Firmar_SinRespuestasNo_NoCreaHallazgos()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario, Si(escenario.Limpieza), Si(escenario.Cables));

        var action = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Firmar_UnNoEscritoEnUnaPreguntaDeTexto_NoEsNovedad()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario,
            Si(escenario.Limpieza), Si(escenario.Cables),
            new RespuestaInspeccion { IdPregunta = escenario.Lectura.IdPregunta, Respuesta = "No" });

        var action = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.False(await escenario.Context.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Firmar_UnaNovedadQueYaTieneHallazgo_NoLaDuplica()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario,
            No(escenario.Limpieza, "Polvo acumulado"), No(escenario.Cables, "Cable pelado"));
        escenario.Context.Hallazgos.Add(new Hallazgo
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = escenario.Limpieza.IdPregunta,
            Descripcion = "Registrado antes",
            Origen = HallazgoOrigenes.Novedad,
            IdUsuarioRegistro = escenario.Tecnico.IdUsuario
        });
        await escenario.Context.SaveChangesAsync();
        escenario.Context.ChangeTracker.Clear();

        var action = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<OkObjectResult>(action.Result);
        var hallazgos = await escenario.Context.Hallazgos.AsNoTracking().ToListAsync();
        Assert.Equal(2, hallazgos.Count);
        Assert.Single(hallazgos, h => h.IdPregunta == escenario.Limpieza.IdPregunta);
        Assert.Single(hallazgos, h => h.IdPregunta == escenario.Cables.IdPregunta);
    }

    [Fact]
    public async Task Firmar_DosVeces_LaSegundaDevuelve409YNoAgregaHallazgos()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario, No(escenario.Limpieza, "Polvo"), Si(escenario.Cables));
        await FirmarAsync(escenario, inspeccion.IdInspeccion);
        escenario.Context.ChangeTracker.Clear();

        var segunda = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(segunda.Result);
        Assert.Equal(1, await escenario.Context.Hallazgos.CountAsync());
    }

    [Fact]
    public async Task Firmar_SiFallaGuardarLosHallazgos_LaInspeccionNoQuedaFirmada()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario, No(escenario.Limpieza, "Polvo"), Si(escenario.Cables));
        escenario.Interceptor.AlGuardarHallazgos = () => throw new InvalidOperationException("Fallo simulado");

        await Assert.ThrowsAsync<InvalidOperationException>(() => FirmarAsync(escenario, inspeccion.IdInspeccion));

        await using var lectura = NuevoContexto(escenario.Raiz, escenario.NombreBase);
        var guardada = await lectura.Inspecciones.SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FirmaDigital);
        Assert.Null(guardada.FechaCierre);
        Assert.False(await lectura.Hallazgos.AnyAsync());
    }

    [Fact]
    public async Task Firmar_OtraFirmaSimultaneaCerroPrimero_Devuelve409()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario, No(escenario.Limpieza, "Polvo"), Si(escenario.Cables));

        // La otra petición cierra la inspección entre la comprobación y el guardado de esta, y la
        // base rechaza los hallazgos repetidos (en SQL Server lo hace el índice único).
        escenario.Interceptor.AlGuardarHallazgos = () =>
        {
            using var otra = NuevoContexto(escenario.Raiz, escenario.NombreBase);
            otra.Inspecciones.Single(i => i.IdInspeccion == inspeccion.IdInspeccion).Estado = InspeccionEstados.Cerrada;
            otra.SaveChanges();
            throw new DbUpdateException("Índice único IX_Hallazgos_IdInspeccion_IdPregunta");
        };

        var action = await FirmarAsync(escenario, inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(action.Result);
    }

    [Fact]
    public async Task Firmar_OtroErrorDeBaseDeDatos_NoSeConvierteEn409()
    {
        var escenario = await SeedAsync();
        var inspeccion = await CrearInspeccionAsync(escenario, No(escenario.Limpieza, "Polvo"), Si(escenario.Cables));
        escenario.Interceptor.AlGuardarHallazgos = () => throw new DbUpdateException("Fallo de la base");

        await Assert.ThrowsAsync<DbUpdateException>(() => FirmarAsync(escenario, inspeccion.IdInspeccion));
    }
}
