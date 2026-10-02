using ECAR.API.Controllers;
using ECAR.API.Configuration;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Pruebas de BE-3: firma, cierre e inmutabilidad (reglas 3, 4 y 6 del SRS).
/// Van en su propio archivo para no chocar con las secciones de BE-0/BE-1/BE-2 en
/// <c>BackendPhaseThreeTests</c>.
/// </summary>
public class BackendPhaseThreeFirmaTests
{
    /// <summary>PNG real de 1x1 en base64: sirve para superar la validación por magic bytes.</summary>
    private const string FirmaPngValida =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

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

    private sealed record Escenario(
        ECARDbContext Context,
        Usuario Tecnico,
        Usuario OtroTecnico,
        Equipo Equipo,
        Checklist Checklist,
        PreguntaChecklist PreguntaSiNo,
        PreguntaChecklist PreguntaTexto);

    private sealed class FakeEvidenciaStorage : IEvidenciaStorage
    {
        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"2026/10/{idInspeccion}/evidencia{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa) => Task.CompletedTask;
    }

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ECARDbContext(options);
    }

    /// <summary>Checklist con una obligatoria Sí/No (la que puede reportar novedad) y una de texto opcional.</summary>
    private static async Task<Escenario> SeedAsync(ECARDbContext context)
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
            CodigoInterno = "EQ-100",
            ActivoFijo = "AF-100",
            NombreEquipo = "Balanza analítica",
            Activo = true
        };
        var preguntaSiNo = new PreguntaChecklist
        {
            Pregunta = "¿El equipo está en buen estado?",
            TipoRespuesta = TiposRespuesta.SiNo,
            Obligatoria = true,
            Orden = 1
        };
        var preguntaTexto = new PreguntaChecklist
        {
            Pregunta = "Lectura del manómetro",
            TipoRespuesta = TiposRespuesta.Texto,
            Obligatoria = false,
            Orden = 2
        };
        var checklist = new Checklist
        {
            Nombre = "Checklist de balanza",
            Version = "2.0",
            Activo = true,
            Preguntas = [preguntaSiNo, preguntaTexto]
        };

        context.AddRange(tecnico, otroTecnico, equipo, checklist);
        await context.SaveChangesAsync();

        return new Escenario(
            context, tecnico, otroTecnico, equipo, checklist, preguntaSiNo, preguntaTexto);
    }

    /// <summary>Crea una inspección en curso con las respuestas indicadas.</summary>
    private static async Task<Inspeccion> CrearInspeccionAsync(
        Escenario escenario,
        params RespuestaInspeccion[] respuestas)
    {
        var inspeccion = new Inspeccion
        {
            IdEquipo = escenario.Equipo.IdEquipo,
            IdChecklist = escenario.Checklist.IdChecklist,
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

    private static InspeccionesController ControladorDe(
        Escenario escenario, Usuario usuario, params string[] roles) =>
        new(escenario.Context, new FakeCurrentUser(
            usuario.IdUsuario, usuario.Nombre, roles.Length == 0 ? ["Técnico"] : roles),
            new InspeccionService(escenario.Context));

    private static EvidenciasController ControladorEvidenciasDe(
        ECARDbContext context,
        ICurrentUser currentUser) =>
        new(context, currentUser, new FakeEvidenciaStorage(), Options.Create(new EvidenciasOptions
        {
            RutaBase = "EvidenciasPruebas",
            TamanoMaximoMB = 5,
            TiposPermitidos = "image/jpeg,image/png"
        }));

    // ---------------------------------------------------------------- cierre exitoso

    [Fact]
    public async Task Firmar_CierraLaInspeccionYLaMarcaConforme()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida,
            Observaciones = "Sin hallazgos"
        });

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal(InspeccionEstados.Cerrada, response.Data!.Estado);
        Assert.Equal(InspeccionResultados.Conforme, response.Data.Resultado);
        Assert.False(response.Data.ConNovedad);
        Assert.True(response.Data.TieneFirma);
        Assert.Equal(64, response.Data.FirmaHash!.Length);

        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.Cerrada, guardada.Estado);
        Assert.Equal(InspeccionResultados.Conforme, guardada.Resultado);
        Assert.NotNull(guardada.FechaCierre);
        Assert.Equal(FirmaPngValida, guardada.FirmaDigital);
        Assert.Equal("Sin hallazgos", guardada.Observaciones);
    }

    [Fact]
    public async Task Firmar_MarcaConNovedadCuandoUnaRespuestaSiNoEsNo()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "No",
            Observacion = "Carcasa golpeada"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(ok.Value);
        Assert.Equal(InspeccionResultados.ConNovedad, response.Data!.Resultado);
        Assert.True(response.Data.ConNovedad);
        Assert.Equal(1, response.Data.TotalNovedades);
    }

    [Fact]
    public async Task Firmar_AceptaLaFirmaConPrefijoDataUrl()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = "data:image/png;base64," + FirmaPngValida
        });

        Assert.IsType<OkObjectResult>(action.Result);
        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        // Se guarda normalizada, sin el prefijo del data URL.
        Assert.Equal(FirmaPngValida, guardada.FirmaDigital);
    }

    // ---------------------------------------------------------------- validaciones

    [Fact]
    public async Task Firmar_RechazaCuandoFaltaUnaObligatoria()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario);
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(badRequest.Value);
        Assert.False(response.Success);
        var error = Assert.Single(response.Errors!);
        Assert.Contains(escenario.PreguntaSiNo.Pregunta, error);

        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FirmaHash);
    }

    [Fact]
    public async Task Firmar_RechazaNovedadSinObservacion()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "No"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(badRequest.Value);
        var error = Assert.Single(response.Errors!);
        Assert.Contains("observación", error, StringComparison.OrdinalIgnoreCase);

        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
    }

    [Theory]
    [InlineData("", "La firma es requerida")]
    [InlineData("no-es-base64!!", "base64")]
    // JPEG válido en base64: es una imagen, pero no un PNG.
    [InlineData("/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAg=", "PNG")]
    public async Task Firmar_RechazaFirmaInvalida(string firma, string textoEsperado)
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = firma
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(badRequest.Value);
        Assert.Contains(textoEsperado, response.Message, StringComparison.OrdinalIgnoreCase);

        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
    }

    [Fact]
    public void ValidarPng_RechazaUnaFirmaMayorDe200Kb()
    {
        var excedida = Convert.ToBase64String(
            new byte[FirmaInspeccion.TamanoMaximoBytes + 1]);

        var valida = FirmaInspeccion.TryValidarPng(excedida, out _, out var error);

        Assert.False(valida);
        Assert.Contains("200 KB", error);
    }

    [Fact]
    public async Task Firmar_RechazaAQuienNoEsElInspectorPropietario()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        // Ni siquiera un Administrador firma por otro: la firma es personal.
        var controller = ControladorDe(escenario, escenario.OtroTecnico, "Administrador");

        var action = await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        Assert.IsType<ForbidResult>(action.Result);
        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
    }

    [Fact]
    public async Task Firmar_DevuelveNotFoundSiLaInspeccionNoExiste()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.FirmarInspeccion(9999, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    // ---------------------------------------------------------------- hash de cierre

    [Fact]
    public async Task Hash_EsReproducibleYCambiaSiSeAlteraUnaRespuesta()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);
        await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });

        var cerrada = await context.Inspecciones
            .AsNoTracking()
            .Include(i => i.Respuestas)
            .Include(i => i.Evidencias)
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);

        // Recalcular con los mismos datos reproduce exactamente el hash guardado.
        var recalculado = FirmaInspeccion.CalcularHash(
            cerrada, cerrada.Respuestas, cerrada.Evidencias,
            cerrada.FechaCierre!.Value, cerrada.FirmaDigital!);
        Assert.Equal(cerrada.FirmaHash, recalculado);

        // Alterar una respuesta rompe la coincidencia: el registro quedaría delatado.
        var alteradas = cerrada.Respuestas
            .Select(r => new RespuestaInspeccion
            {
                IdRespuesta = r.IdRespuesta,
                IdInspeccion = r.IdInspeccion,
                IdPregunta = r.IdPregunta,
                Respuesta = "No",
                Observacion = r.Observacion
            })
            .ToList();
        var hashAlterado = FirmaInspeccion.CalcularHash(
            cerrada, alteradas, cerrada.Evidencias,
            cerrada.FechaCierre.Value, cerrada.FirmaDigital!);

        Assert.NotEqual(cerrada.FirmaHash, hashAlterado);
    }

    // ---------------------------------------------------------------- inmutabilidad

    [Fact]
    public async Task Firmar_UnaSegundaVezDevuelve409()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);
        var dto = new FirmarInspeccionDto { FirmaPngBase64 = FirmaPngValida };

        var primera = await controller.FirmarInspeccion(inspeccion.IdInspeccion, dto);
        Assert.IsType<OkObjectResult>(primera.Result);
        var hashOriginal = (await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion)).FirmaHash;

        var segunda = await controller.FirmarInspeccion(inspeccion.IdInspeccion, dto);

        var conflicto = Assert.IsType<ConflictObjectResult>(segunda.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(conflicto.Value);
        Assert.False(response.Success);
        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.Equal(hashOriginal, guardada.FirmaHash);
    }

    [Fact]
    public async Task ActualizarInspeccionCerrada_Devuelve409YNoCambiaNada()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);
        await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });
        context.ChangeTracker.Clear();

        var action = await controller.UpdateInspeccion(inspeccion.IdInspeccion, new UpdateInspeccionDto
        {
            Observaciones = "Intento de edición posterior",
            FirmaDigital = "otra-firma"
        });

        Assert.IsType<ConflictObjectResult>(action.Result);
        var guardada = await context.Inspecciones.AsNoTracking()
            .SingleAsync(i => i.IdInspeccion == inspeccion.IdInspeccion);
        Assert.NotEqual("Intento de edición posterior", guardada.Observaciones);
        Assert.Equal(FirmaPngValida, guardada.FirmaDigital);
    }

    [Fact]
    public async Task EliminarInspeccionCerrada_Devuelve409()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var controller = ControladorDe(escenario, escenario.Tecnico);
        await controller.FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
        {
            FirmaPngBase64 = FirmaPngValida
        });
        context.ChangeTracker.Clear();

        var action = await controller.DeleteInspeccion(inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.True(await context.Inspecciones
            .AnyAsync(i => i.IdInspeccion == inspeccion.IdInspeccion));
    }

    [Fact]
    public async Task AgregarEvidenciaAInspeccionCerrada_Devuelve409()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var currentUser = new FakeCurrentUser(
            escenario.Tecnico.IdUsuario, escenario.Tecnico.Nombre, "Técnico");
        await ControladorDe(escenario, escenario.Tecnico)
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
            {
                FirmaPngBase64 = FirmaPngValida
            });
        context.ChangeTracker.Clear();

        var contenido = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
        await using var stream = new MemoryStream(contenido);
        var archivo = new FormFile(stream, 0, contenido.Length, "archivo", "foto.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
        var action = await ControladorEvidenciasDe(context, currentUser)
            .CreateEvidencia(inspeccion.IdInspeccion, archivo);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Empty(await context.Evidencias.ToListAsync());
    }

    [Fact]
    public async Task EliminarEvidenciaDeInspeccionCerrada_Devuelve409()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        var evidencia = new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = "2026/09/1/foto.jpg",
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 1024,
            IdUsuarioCarga = escenario.Tecnico.IdUsuario
        };
        context.Evidencias.Add(evidencia);
        await context.SaveChangesAsync();
        var currentUser = new FakeCurrentUser(
            escenario.Tecnico.IdUsuario, escenario.Tecnico.Nombre, "Técnico");
        context.ChangeTracker.Clear();
        await ControladorDe(escenario, escenario.Tecnico)
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
            {
                FirmaPngBase64 = FirmaPngValida
            });
        context.ChangeTracker.Clear();

        var action = await ControladorEvidenciasDe(context, currentUser)
            .DeleteEvidencia(evidencia.IdEvidencia);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.True(await context.Evidencias
            .AnyAsync(e => e.IdEvidencia == evidencia.IdEvidencia));
    }

    // ---------------------------------------------------------------- resultado

    [Fact]
    public async Task GetResultado_DevuelveElCierreCompletoAlAuditor()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "No",
            Observacion = "Fuga en el sello"
        });
        await ControladorDe(escenario, escenario.Tecnico)
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
            {
                FirmaPngBase64 = FirmaPngValida
            });
        context.ChangeTracker.Clear();
        var auditor = ControladorDe(escenario, escenario.OtroTecnico, "Auditor");

        var action = await auditor.GetResultado(inspeccion.IdInspeccion);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<ApiResponse<InspeccionResultadoDto>>(ok.Value);
        var resultado = response.Data!;
        Assert.Equal(InspeccionEstados.Cerrada, resultado.Estado);
        Assert.Equal(InspeccionResultados.ConNovedad, resultado.Resultado);
        Assert.Equal("EQ-100", resultado.CodigoInterno);
        Assert.Equal("2.0", resultado.VersionChecklist);
        Assert.Equal(escenario.Tecnico.Nombre, resultado.NombreUsuario);
        Assert.Equal(FirmaPngValida, resultado.FirmaPngBase64);
        Assert.NotNull(resultado.FirmaHash);
        Assert.Equal([1, 2], resultado.Preguntas.Select(p => p.Orden).ToArray());
        Assert.Equal("Fuga en el sello", resultado.Preguntas[0].Observacion);
    }

    [Fact]
    public async Task GetResultado_Devuelve409SiLaInspeccionSigueEnCurso()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario);
        var controller = ControladorDe(escenario, escenario.Tecnico);

        var action = await controller.GetResultado(inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(action.Result);
    }

    [Fact]
    public async Task GetResultado_RechazaAUnTecnicoAjeno()
    {
        await using var context = CreateContext();
        var escenario = await SeedAsync(context);
        var inspeccion = await CrearInspeccionAsync(escenario, new RespuestaInspeccion
        {
            IdPregunta = escenario.PreguntaSiNo.IdPregunta,
            Respuesta = "Si"
        });
        await ControladorDe(escenario, escenario.Tecnico)
            .FirmarInspeccion(inspeccion.IdInspeccion, new FirmarInspeccionDto
            {
                FirmaPngBase64 = FirmaPngValida
            });
        context.ChangeTracker.Clear();

        var action = await ControladorDe(escenario, escenario.OtroTecnico)
            .GetResultado(inspeccion.IdInspeccion);

        Assert.IsType<ForbidResult>(action.Result);
    }
}
