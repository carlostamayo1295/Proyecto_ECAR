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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECAR.API.Tests;

public class FirmaIntegridadTests
{
    private const string FirmaPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private sealed class UsuarioActual(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Pruebas";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class AlmacenEnMemoria : IEvidenciaStorage
    {
        public Dictionary<string, byte[]> Archivos { get; } = new();

        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion)
        {
            var ruta = $"{idInspeccion}/{Guid.NewGuid():N}{extension}";
            using var copia = new MemoryStream();
            stream.CopyTo(copia);
            Archivos[ruta] = copia.ToArray();
            return Task.FromResult(ruta);
        }

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Archivos.TryGetValue(rutaRelativa, out var contenido)
                ? Task.FromResult<Stream>(new MemoryStream(contenido))
                : throw new FileNotFoundException(rutaRelativa);

        public Task EliminarAsync(string rutaRelativa)
        {
            Archivos.Remove(rutaRelativa);
            return Task.CompletedTask;
        }
    }

    private sealed record Escenario(ECARDbContext Context, Usuario Tecnico, Inspeccion Inspeccion, AlmacenEnMemoria Almacen);

    private static async Task<Escenario> CrearEscenarioAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var almacen = new AlmacenEnMemoria();

        var tecnico = new Usuario
        {
            Nombre = "Técnico Firmante",
            Correo = "firmante@ecar.test",
            PasswordHash = FirmaPruebas.PasswordHash,
            Activo = true
        };
        var equipo = new Equipo { CodigoInterno = "EQ-200", ActivoFijo = "AF-200", NombreEquipo = "Autoclave", Activo = true };
        var pregunta = new PreguntaChecklist
        {
            Pregunta = "¿Sella correctamente?",
            TipoRespuesta = TiposRespuesta.SiNo,
            Obligatoria = true,
            Orden = 1
        };
        var checklist = new Checklist { Nombre = "Autoclaves", Version = "1.0", Activo = true, Preguntas = [pregunta] };
        context.AddRange(tecnico, equipo, checklist);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdChecklist = checklist.IdChecklist,
            IdUsuario = tecnico.IdUsuario,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var ruta = await almacen.GuardarAsync(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]), ".jpg", inspeccion.IdInspeccion);
        context.RespuestasInspeccion.Add(new RespuestaInspeccion
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "Si"
        });
        context.Evidencias.Add(new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = ruta,
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 7,
            IdUsuarioCarga = tecnico.IdUsuario
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, tecnico, inspeccion, almacen);
    }

    private static InspeccionesController Controlador(Escenario escenario, long idUsuario, params string[] roles) =>
        new(escenario.Context, new UsuarioActual(idUsuario, roles.Length == 0 ? ["Técnico"] : roles),
            new InspeccionService(escenario.Context), escenario.Almacen, NullLogger<InspeccionesController>.Instance);

    private static Task<ActionResult<ApiResponse<InspeccionResultadoDto>>> FirmarAsync(Escenario escenario, string? password) =>
        Controlador(escenario, escenario.Tecnico.IdUsuario).FirmarInspeccion(
            escenario.Inspeccion.IdInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = FirmaPng, Password = password },
            FirmaPruebas.CrearAuthService(escenario.Context),
            FirmaPruebas.CrearVerificador(escenario.Almacen));

    private static async Task<IntegridadInspeccionDto> VerificarAsync(Escenario escenario, string clave = FirmaPruebas.ClaveHmac)
    {
        escenario.Context.ChangeTracker.Clear();
        var resultado = await Controlador(escenario, 99, "Auditor").VerificarIntegridad(
            escenario.Inspeccion.IdInspeccion, FirmaPruebas.CrearVerificador(escenario.Almacen, clave));
        var ok = Assert.IsType<OkObjectResult>(resultado.Result);
        return Assert.IsType<ApiResponse<IntegridadInspeccionDto>>(ok.Value).Data!;
    }

    private Task<Inspeccion> LeerAsync(Escenario escenario) => escenario.Context.Inspecciones.AsNoTracking()
        .SingleAsync(i => i.IdInspeccion == escenario.Inspeccion.IdInspeccion);

    [Fact]
    public async Task Firmar_ConPasswordIncorrectaDevuelve400YNoCierra()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;

        var resultado = await FirmarAsync(escenario, "incorrecta");

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        var guardada = await LeerAsync(escenario);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FirmaHash);
    }

    [Fact]
    public async Task Firmar_SinPasswordDevuelve400()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;

        var resultado = await FirmarAsync(escenario, null);

        Assert.IsType<BadRequestObjectResult>(resultado.Result);
        Assert.Equal(InspeccionEstados.EnCurso, (await LeerAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Firmar_ConLaCuentaBloqueadaDevuelve423YNoCierra()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;

        for (var intento = 0; intento < 4; intento++)
        {
            Assert.IsType<BadRequestObjectResult>((await FirmarAsync(escenario, "incorrecta")).Result);
            escenario.Context.ChangeTracker.Clear();
        }

        var quinto = await FirmarAsync(escenario, "incorrecta");
        escenario.Context.ChangeTracker.Clear();
        var conLaCorrecta = await FirmarAsync(escenario, FirmaPruebas.Password);

        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(quinto.Result).StatusCode);
        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(conLaCorrecta.Result).StatusCode);
        Assert.Equal(InspeccionEstados.EnCurso, (await LeerAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Firmar_GuardaNombreFechaSignificadoYAlgoritmo()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;

        var resultado = await FirmarAsync(escenario, FirmaPruebas.Password);

        var dto = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
        Assert.Equal("Técnico Firmante", dto.FirmaNombre);
        Assert.Equal(VerificadorIntegridad.SignificadoEjecucion, dto.FirmaSignificado);
        Assert.Equal(VerificadorIntegridad.AlgoritmoHmac, dto.FirmaAlgoritmo);

        var guardada = await LeerAsync(escenario);
        Assert.Equal(InspeccionEstados.Cerrada, guardada.Estado);
        Assert.NotNull(guardada.FechaCierre);
        Assert.Equal("Técnico Firmante", guardada.FirmaNombre);
        Assert.Equal(VerificadorIntegridad.AlgoritmoHmac, guardada.FirmaAlgoritmo);
        Assert.Equal(64, guardada.FirmaHash!.Length);
    }

    [Fact]
    public async Task Integridad_DeUnaInspeccionSinCambiosEsCorrecta()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);

        var integridad = await VerificarAsync(escenario);

        Assert.True(integridad.Integra);
        Assert.Equal(VerificadorIntegridad.AlgoritmoHmac, integridad.Algoritmo);
        Assert.Contains(await escenario.Context.Auditoria.AsNoTracking().ToListAsync(),
            a => a.Accion == AuditoriaAcciones.VerificacionIntegridad);
    }

    [Fact]
    public async Task Integridad_FallaSiSeAlteraUnaRespuesta()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);
        escenario.Context.ChangeTracker.Clear();

        var respuesta = await escenario.Context.RespuestasInspeccion.SingleAsync();
        respuesta.Respuesta = "No";
        await escenario.Context.SaveChangesAsync();

        Assert.False((await VerificarAsync(escenario)).Integra);
    }

    [Fact]
    public async Task Integridad_FallaSiSeAlteraLaFotoDeUnaEvidencia()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);

        var ruta = escenario.Almacen.Archivos.Keys.Single();
        escenario.Almacen.Archivos[ruta] = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9, 9];

        Assert.False((await VerificarAsync(escenario)).Integra);
    }

    [Fact]
    public async Task Integridad_FallaSiSeAlteranLosDatosDeUnaEvidencia()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);
        escenario.Context.ChangeTracker.Clear();

        var evidencia = await escenario.Context.Evidencias.SingleAsync();
        evidencia.Retirada = true;
        await escenario.Context.SaveChangesAsync();

        Assert.False((await VerificarAsync(escenario)).Integra);
    }

    [Fact]
    public async Task Integridad_FallaConOtraClave()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);

        Assert.False((await VerificarAsync(escenario, "otra-clave-distinta-de-mas-de-32-caracteres-xx")).Integra);
    }

    [Fact]
    public async Task Integridad_VerificaLasFirmasAnterioresConSha256()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        var inspeccion = await escenario.Context.Inspecciones
            .Include(i => i.Respuestas)
            .Include(i => i.Evidencias)
            .SingleAsync();
        var fechaCierre = DateTime.UtcNow;
        inspeccion.Estado = InspeccionEstados.Cerrada;
        inspeccion.Resultado = InspeccionResultados.Conforme;
        inspeccion.FechaCierre = fechaCierre;
        inspeccion.FirmaDigital = FirmaPng;
        inspeccion.FirmaHash = FirmaInspeccion.CalcularHash(inspeccion, inspeccion.Respuestas, inspeccion.Evidencias,
            fechaCierre, FirmaPng);
        await escenario.Context.SaveChangesAsync();

        var integridad = await VerificarAsync(escenario);
        Assert.True(integridad.Integra);
        Assert.Equal(VerificadorIntegridad.AlgoritmoLegado, integridad.Algoritmo);

        escenario.Context.ChangeTracker.Clear();
        var respuesta = await escenario.Context.RespuestasInspeccion.SingleAsync();
        respuesta.Observacion = "Alterada";
        await escenario.Context.SaveChangesAsync();
        Assert.False((await VerificarAsync(escenario)).Integra);
    }

    [Fact]
    public async Task Integridad_DeUnaInspeccionEnCursoDevuelve409()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;

        var resultado = await Controlador(escenario, 99, "Auditor").VerificarIntegridad(
            escenario.Inspeccion.IdInspeccion, FirmaPruebas.CrearVerificador(escenario.Almacen));

        Assert.IsType<ConflictObjectResult>(resultado.Result);
    }

    [Fact]
    public async Task InspeccionFirmada_NoAdmiteCambios()
    {
        var escenario = await CrearEscenarioAsync();
        await using var _ = escenario.Context;
        await FirmarAsync(escenario, FirmaPruebas.Password);
        escenario.Context.ChangeTracker.Clear();

        var actualizar = await Controlador(escenario, escenario.Tecnico.IdUsuario)
            .UpdateInspeccion(escenario.Inspeccion.IdInspeccion, new UpdateInspeccionDto { Observaciones = "cambio" });
        escenario.Context.ChangeTracker.Clear();
        var segundaFirma = await FirmarAsync(escenario, FirmaPruebas.Password);

        Assert.IsType<ConflictObjectResult>(actualizar.Result);
        Assert.IsType<ConflictObjectResult>(segundaFirma.Result);
        Assert.True((await VerificarAsync(escenario)).Integra);
    }
}
