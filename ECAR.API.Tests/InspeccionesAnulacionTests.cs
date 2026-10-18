using System.Security.Claims;
using ECAR.API.Configuration;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 2 de la anulación: POST api/inspecciones/{id}/anular sustituye al borrado. Solo se anula
/// una inspección EnCurso, con motivo, por el Administrador o su inspector; después la inspección
/// es inmutable como una cerrada.
/// </summary>
public class InspeccionesAnulacionTests
{
    private const string MotivoValido = "Se inició sobre el equipo equivocado";

    private const string FirmaPngValida =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Usuario";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FakeStorage : IEvidenciaStorage
    {
        public List<string> Eliminados { get; } = new();

        public Task<string> GuardarAsync(Stream stream, string extension, long idInspeccion) =>
            Task.FromResult($"2026/10/{idInspeccion}/nueva{extension}");

        public Task<Stream> AbrirAsync(string rutaRelativa) =>
            Task.FromResult<Stream>(new MemoryStream([0xFF, 0xD8, 0xFF, 0x00]));

        public Task EliminarAsync(string rutaRelativa)
        {
            Eliminados.Add(rutaRelativa);
            return Task.CompletedTask;
        }
    }

    /// <summary>Como ContextoAuditoriaHttp: el motivo de la auditoría es el que fijó el controlador.</summary>
    private sealed class ContextoAuditoriaFalso(IMotivoCambio motivoCambio) : IContextoAuditoria
    {
        public long? IdUsuario => null;
        public string Usuario => "Pruebas";
        public string? DireccionIp => null;
        public string? AgenteUsuario => null;
        public string? Motivo => motivoCambio.Motivo;
    }

    /// <summary>Ejecuta <see cref="AlGuardar"/> justo antes de guardar una inspección modificada.</summary>
    private sealed class InterceptorQueFalla : SaveChangesInterceptor
    {
        public Action? AlGuardar { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<Inspeccion>().Any(en => en.State == EntityState.Modified))
            {
                AlGuardar?.Invoke();
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed record Escenario(ECARDbContext Context, string NombreBase, MotivoCambio Motivo,
        InterceptorQueFalla Interceptor, FakeStorage Storage, Usuario Admin, Usuario Tecnico, Usuario OtroTecnico,
        PreguntaChecklist Pregunta, Inspeccion Inspeccion, Evidencia Evidencia);

    private static ECARDbContext NuevoContexto(string nombre, IContextoAuditoria? contexto = null,
        IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(nombre);
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new ECARDbContext(builder.Options, contexto);
    }

    /// <summary>Una inspección del técnico con una respuesta, una evidencia y un hallazgo.</summary>
    private static async Task<Escenario> SeedAsync(string estado = InspeccionEstados.EnCurso)
    {
        var nombre = Guid.NewGuid().ToString();
        var motivo = new MotivoCambio();
        var interceptor = new InterceptorQueFalla();
        var context = NuevoContexto(nombre, new ContextoAuditoriaFalso(motivo), interceptor);

        var admin = new Usuario { Nombre = "Admin", Correo = "admin@ecar.test", PasswordHash = "hash", Activo = true };
        var tecnico = new Usuario { Nombre = "Ana Técnica", Correo = "ana@ecar.test", PasswordHash = FirmaPruebas.PasswordHash, Activo = true };
        var otro = new Usuario { Nombre = "Otro", Correo = "otro@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = "EQ-001", ActivoFijo = "AF-001", NombreEquipo = "Balanza", Activo = true };
        var pregunta = new PreguntaChecklist { Pregunta = "¿Está limpio?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true, Preguntas = [pregunta] };
        context.AddRange(admin, tecnico, otro, equipo, checklist);
        await context.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = tecnico.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = estado
        };
        context.Inspecciones.Add(inspeccion);
        await context.SaveChangesAsync();

        var evidencia = new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = "2026/10/1/a.jpg",
            NombreOriginal = "a.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 4,
            FechaCarga = DateTime.UtcNow,
            IdUsuarioCarga = tecnico.IdUsuario
        };
        context.AddRange(
            new RespuestaInspeccion { IdInspeccion = inspeccion.IdInspeccion, IdPregunta = pregunta.IdPregunta, Respuesta = "Si" },
            evidencia,
            new Hallazgo
            {
                IdInspeccion = inspeccion.IdInspeccion,
                Descripcion = "Fuga",
                Origen = HallazgoOrigenes.Manual,
                IdUsuarioRegistro = tecnico.IdUsuario
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, nombre, motivo, interceptor, new FakeStorage(), admin, tecnico, otro,
            pregunta, inspeccion, evidencia);
    }

    private static InspeccionesController Como(Escenario e, Usuario usuario, string rol = "Técnico") =>
        new(e.Context, new FakeCurrentUser(usuario.IdUsuario, rol), new InspeccionService(e.Context), e.Storage,
            NullLogger<InspeccionesController>.Instance, e.Motivo);

    private static EvidenciasController EvidenciasComo(Escenario e, Usuario usuario)
    {
        var controller = new EvidenciasController(e.Context, new FakeCurrentUser(usuario.IdUsuario, "Técnico"), e.Storage,
            Options.Create(new EvidenciasOptions { RutaBase = "EvidenciasPruebas", TamanoMaximoMB = 5, TiposPermitidos = "image/jpeg,image/png" }),
            e.Motivo);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Técnico")], "Pruebas"))
            }
        };
        return controller;
    }

    private static Task<ActionResult<ApiResponse<InspeccionDto>>> AnularAsync(Escenario e, Usuario usuario,
        string motivo = MotivoValido, string rol = "Técnico") =>
        Como(e, usuario, rol).AnularInspeccion(e.Inspeccion.IdInspeccion, new MotivoDto { Motivo = motivo });

    private static Task<Inspeccion> GuardadaAsync(Escenario e) =>
        e.Context.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == e.Inspeccion.IdInspeccion);

    private static async Task<Escenario> AnuladaAsync()
    {
        var e = await SeedAsync();
        Assert.IsType<OkObjectResult>((await AnularAsync(e, e.Tecnico)).Result);
        e.Context.ChangeTracker.Clear();
        return e;
    }

    private static string Mensaje<T>(ActionResult<ApiResponse<T>> action) =>
        Assert.IsType<ApiResponse<T>>(Assert.IsAssignableFrom<ObjectResult>(action.Result).Value).Message;

    // ------------------------------------------------------------------ anular

    [Fact]
    public async Task Anular_ElInspectorConMotivo_LaDejaAnuladaConMotivoFechaYUsuario()
    {
        var e = await SeedAsync();

        var action = await AnularAsync(e, e.Tecnico, motivo: "  " + MotivoValido + "  ");

        var dto = Assert.IsType<ApiResponse<InspeccionDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(InspeccionEstados.Anulada, dto.Estado);
        Assert.Equal(MotivoValido, dto.MotivoAnulacion);
        Assert.Equal(e.Tecnico.IdUsuario, dto.IdUsuarioAnulacion);
        Assert.Equal("Ana Técnica", dto.NombreUsuarioAnulacion);
        Assert.NotNull(dto.FechaAnulacion);

        var guardada = await GuardadaAsync(e);
        Assert.Equal(InspeccionEstados.Anulada, guardada.Estado);
        Assert.Equal(MotivoValido, guardada.MotivoAnulacion);
        Assert.Equal(e.Tecnico.IdUsuario, guardada.IdUsuarioAnulacion);
        Assert.NotNull(guardada.FechaAnulacion);
    }

    [Fact]
    public async Task Anular_ElAdministrador_AnulaLaDeOtroUsuario()
    {
        var e = await SeedAsync();

        var action = await AnularAsync(e, e.Admin, rol: "Administrador");

        Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal(e.Admin.IdUsuario, (await GuardadaAsync(e)).IdUsuarioAnulacion);
    }

    [Fact]
    public async Task Anular_QuedaEnLaAuditoriaComoAnularConSuMotivo()
    {
        var e = await SeedAsync();

        await AnularAsync(e, e.Tecnico);

        var fila = await e.Context.Auditoria.AsNoTracking()
            .Where(a => a.Tabla == "Inspecciones" && a.RegistroId == e.Inspeccion.IdInspeccion)
            .OrderByDescending(a => a.IdAuditoria)
            .FirstAsync();
        Assert.Equal(AuditoriaAcciones.Anular, fila.Accion);
        Assert.Equal(MotivoValido, fila.Motivo);
        Assert.Contains(InspeccionEstados.Anulada, fila.ValorNuevo);
    }

    [Fact]
    public async Task Anular_ConservaRespuestasEvidenciasFotosYHallazgos()
    {
        var e = await AnuladaAsync();

        Assert.Equal(1, await e.Context.RespuestasInspeccion.CountAsync(r => r.IdInspeccion == e.Inspeccion.IdInspeccion));
        Assert.Equal(1, await e.Context.Evidencias.CountAsync(ev => ev.IdInspeccion == e.Inspeccion.IdInspeccion));
        Assert.Equal(1, await e.Context.Hallazgos.CountAsync(h => h.IdInspeccion == e.Inspeccion.IdInspeccion));
        Assert.Empty(e.Storage.Eliminados);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("Corto")]
    public async Task Anular_SinMotivoValido_Devuelve400YNoAnula(string motivo)
    {
        var e = await SeedAsync();

        var action = await AnularAsync(e, e.Tecnico, motivo);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(InspeccionesController.MensajeMotivoRequerido, Mensaje(action));
        var guardada = await GuardadaAsync(e);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.MotivoAnulacion);
    }

    [Theory]
    [InlineData(InspeccionEstados.Cerrada)]
    [InlineData(InspeccionEstados.Anulada)]
    public async Task Anular_UnaInspeccionQueNoEstaEnCurso_Devuelve409(string estado)
    {
        var e = await SeedAsync(estado);

        var action = await AnularAsync(e, e.Admin, rol: "Administrador");

        Assert.IsType<ConflictObjectResult>(action.Result);
        var guardada = await GuardadaAsync(e);
        Assert.Equal(estado, guardada.Estado);
        Assert.Null(guardada.IdUsuarioAnulacion);
    }

    [Fact]
    public async Task Anular_UnTecnicoQueNoEsElInspector_Devuelve403()
    {
        var e = await SeedAsync();

        var action = await AnularAsync(e, e.OtroTecnico);

        Assert.IsType<ForbidResult>(action.Result);
        Assert.Equal(InspeccionEstados.EnCurso, (await GuardadaAsync(e)).Estado);
    }

    [Fact]
    public async Task Anular_UnaInspeccionInexistente_Devuelve404()
    {
        var e = await SeedAsync();

        var action = await Como(e, e.Admin, "Administrador").AnularInspeccion(999, new MotivoDto { Motivo = MotivoValido });

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }

    [Fact]
    public async Task Anular_SiFallaElGuardado_LaInspeccionSigueEnCurso()
    {
        var e = await SeedAsync();
        e.Interceptor.AlGuardar = () => throw new DbUpdateException("Fallo simulado al guardar");

        await Assert.ThrowsAsync<DbUpdateException>(() => AnularAsync(e, e.Tecnico));

        await using var lectura = NuevoContexto(e.NombreBase);
        var guardada = await lectura.Inspecciones.SingleAsync(i => i.IdInspeccion == e.Inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.MotivoAnulacion);
    }

    // ------------------------------------------------------------------ una anulada no admite cambios

    [Fact]
    public async Task Anulada_NoSePuedeFirmar()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).FirmarConPassword(e.Context, e.Inspeccion.IdInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = FirmaPngValida });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
        Assert.Equal(InspeccionEstados.Anulada, (await GuardadaAsync(e)).Estado);
    }

    [Fact]
    public async Task Anulada_NoSePuedeEditar()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).UpdateInspeccion(e.Inspeccion.IdInspeccion,
            new UpdateInspeccionDto { Observaciones = "Cambio" });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
        Assert.Null((await GuardadaAsync(e)).Observaciones);
    }

    [Fact]
    public async Task Anulada_NoAdmiteRespuestas()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).GuardarRespuestas(e.Inspeccion.IdInspeccion, new GuardarRespuestasDto
        {
            Respuestas = [new RespuestaEjecucionDto { IdPregunta = e.Pregunta.IdPregunta, Respuesta = "No", Observacion = "X" }]
        });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
        Assert.Equal("Si", (await e.Context.RespuestasInspeccion.AsNoTracking().SingleAsync()).Respuesta);
    }

    [Fact]
    public async Task Anulada_NoAdmiteEvidenciasNuevas()
    {
        var e = await AnuladaAsync();
        var foto = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x01 };
        var archivo = new FormFile(new MemoryStream(foto), 0, foto.Length, "archivo", "foto.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var action = await EvidenciasComo(e, e.Tecnico).CreateEvidencia(e.Inspeccion.IdInspeccion, archivo);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
        Assert.Equal(1, await e.Context.Evidencias.CountAsync());
    }

    [Fact]
    public async Task Anulada_NoSeLeRetiranEvidencias()
    {
        var e = await AnuladaAsync();

        var action = await EvidenciasComo(e, e.Tecnico).RetirarEvidencia(e.Evidencia.IdEvidencia,
            new MotivoDto { Motivo = MotivoValido });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
        Assert.Equal(1, await e.Context.Evidencias.CountAsync());
        Assert.Empty(e.Storage.Eliminados);
    }

    [Fact]
    public async Task Anulada_NoTieneResultadoFirmado()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).GetResultado(e.Inspeccion.IdInspeccion);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Contains("anulada", Mensaje(action));
    }

    [Fact]
    public async Task Anulada_SeSigueConsultandoSuEjecucion()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).GetEjecucion(e.Inspeccion.IdInspeccion);

        var dto = Assert.IsType<ApiResponse<InspeccionEjecucionDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(InspeccionEstados.Anulada, dto.Estado);
        Assert.Single(dto.Preguntas, p => p.Respuesta == "Si");
        Assert.Single(dto.Evidencias);
    }

    [Fact]
    public async Task Anulada_NoAdmiteHallazgosManuales()
    {
        var e = await AnuladaAsync();

        var action = await new HallazgosController(e.Context, new FakeCurrentUser(e.Tecnico.IdUsuario, "Técnico"), e.Motivo)
            .CreateHallazgo(new CreateHallazgoDto { IdInspeccion = e.Inspeccion.IdInspeccion, Descripcion = "Nuevo" });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(1, await e.Context.Hallazgos.CountAsync());
    }

    // ------------------------------------------------------------------ Fase 3: conservación

    /// <summary>Todo lo que cuelga de la inspección, en texto, para comparar antes y después.</summary>
    private static async Task<string> InstantaneaAsync(Escenario e)
    {
        var id = e.Inspeccion.IdInspeccion;
        var respuestas = await e.Context.RespuestasInspeccion.AsNoTracking().Where(r => r.IdInspeccion == id)
            .OrderBy(r => r.IdRespuesta).Select(r => $"{r.IdRespuesta}|{r.IdPregunta}|{r.Respuesta}|{r.Observacion}").ToListAsync();
        var evidencias = await e.Context.Evidencias.AsNoTracking().Where(ev => ev.IdInspeccion == id)
            .OrderBy(ev => ev.IdEvidencia).Select(ev => $"{ev.IdEvidencia}|{ev.Archivo}|{ev.NombreOriginal}|{ev.TamanoBytes}|{ev.Retirada}").ToListAsync();
        var hallazgos = await e.Context.Hallazgos.AsNoTracking().Where(h => h.IdInspeccion == id)
            .OrderBy(h => h.IdHallazgo).Select(h => $"{h.IdHallazgo}|{h.Descripcion}|{h.Estado}|{h.Origen}").ToListAsync();
        var inspeccion = await e.Context.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == id);
        return string.Join("\n", [
            $"{inspeccion.IdEquipo}|{inspeccion.IdChecklist}|{inspeccion.IdUsuario}|{inspeccion.FechaInspeccion:O}|{inspeccion.Observaciones}",
            .. respuestas, .. evidencias, .. hallazgos]);
    }

    [Fact]
    public async Task Anulada_ConservaElContenidoExactoDeTodoLoQueTenia()
    {
        var e = await SeedAsync();
        var antes = await InstantaneaAsync(e);

        await AnularAsync(e, e.Tecnico);
        e.Context.ChangeTracker.Clear();

        Assert.Equal(antes, await InstantaneaAsync(e));
        Assert.Empty(e.Storage.Eliminados);
    }

    [Fact]
    public async Task Anulada_SusHallazgosSiguenConsultablesYGestionables()
    {
        var e = await AnuladaAsync();
        var hallazgos = new HallazgosController(e.Context, new FakeCurrentUser(e.Tecnico.IdUsuario, "Técnico"), e.Motivo);
        var idHallazgo = (await e.Context.Hallazgos.AsNoTracking().SingleAsync()).IdHallazgo;

        var listado = await hallazgos.GetHallazgos(new HallazgoFiltroDto { IdInspeccion = e.Inspeccion.IdInspeccion });
        var cambio = await hallazgos.CambiarEstado(idHallazgo, new CambiarEstadoHallazgoDto { Estado = HallazgoEstados.EnProceso });

        var pagina = Assert.IsType<ApiResponse<PagedResultDto<HallazgoDto>>>(Assert.IsType<OkObjectResult>(listado.Result).Value).Data!;
        Assert.Equal(1, pagina.TotalCount);
        Assert.IsType<OkObjectResult>(cambio.Result);
        Assert.Equal(HallazgoEstados.EnProceso, (await e.Context.Hallazgos.AsNoTracking().SingleAsync()).Estado);
    }

    [Fact]
    public async Task Anulada_NoImpideIniciarOtraInspeccionDelMismoEquipo()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).IniciarInspeccion(new IniciarInspeccionDto
        {
            IdEquipo = e.Inspeccion.IdEquipo,
            IdChecklist = e.Inspeccion.IdChecklist
        });

        Assert.IsType<CreatedAtActionResult>(action.Result);
        Assert.Equal(2, await e.Context.Inspecciones.CountAsync());
    }

    // ------------------------------------------------------------------ Fase 3: consultas

    private static async Task<Inspeccion> OtraEnCursoAsync(Escenario e)
    {
        var otra = new Inspeccion
        {
            IdEquipo = e.Inspeccion.IdEquipo,
            IdUsuario = e.Tecnico.IdUsuario,
            IdChecklist = e.Inspeccion.IdChecklist,
            FechaInspeccion = DateTime.UtcNow.AddMinutes(5),
            Estado = InspeccionEstados.EnCurso
        };
        e.Context.Inspecciones.Add(otra);
        await e.Context.SaveChangesAsync();
        e.Context.ChangeTracker.Clear();
        return otra;
    }

    [Fact]
    public async Task Listado_SinFiltro_IncluyeLasAnuladasConSusDatosDeAnulacion()
    {
        var e = await AnuladaAsync();
        await OtraEnCursoAsync(e);

        var action = await Como(e, e.Admin, "Administrador").GetInspecciones();

        var pagina = Assert.IsType<ApiResponse<PagedResultDto<InspeccionDto>>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(2, pagina.TotalCount);
        var anulada = pagina.Data.Single(i => i.IdInspeccion == e.Inspeccion.IdInspeccion);
        Assert.Equal(InspeccionEstados.Anulada, anulada.Estado);
        Assert.Equal(MotivoValido, anulada.MotivoAnulacion);
        Assert.Equal("Ana Técnica", anulada.NombreUsuarioAnulacion);
        Assert.NotNull(anulada.FechaAnulacion);
        Assert.Equal(1, anulada.TotalEvidencias);
        Assert.Equal(1, anulada.TotalHallazgos);
        Assert.Null(pagina.Data.Single(i => i.IdInspeccion != e.Inspeccion.IdInspeccion).MotivoAnulacion);
    }

    [Theory]
    [InlineData(InspeccionEstados.Anulada, true)]
    [InlineData(InspeccionEstados.EnCurso, false)]
    public async Task Listado_PorEstado_SeparaLasAnuladas(string estado, bool esLaAnulada)
    {
        var e = await AnuladaAsync();
        var otra = await OtraEnCursoAsync(e);

        var action = await Como(e, e.Admin, "Administrador").GetInspecciones(estado: estado);

        var pagina = Assert.IsType<ApiResponse<PagedResultDto<InspeccionDto>>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(esLaAnulada ? e.Inspeccion.IdInspeccion : otra.IdInspeccion, pagina.Data.Single().IdInspeccion);
    }

    [Fact]
    public async Task Detalle_DeUnaAnulada_TraeSusDatosDeAnulacion()
    {
        var e = await AnuladaAsync();

        var action = await Como(e, e.Tecnico).GetInspeccion(e.Inspeccion.IdInspeccion);

        var dto = Assert.IsType<ApiResponse<InspeccionDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(InspeccionEstados.Anulada, dto.Estado);
        Assert.Equal(MotivoValido, dto.MotivoAnulacion);
        Assert.Equal(e.Tecnico.IdUsuario, dto.IdUsuarioAnulacion);
        Assert.Equal("Ana Técnica", dto.NombreUsuarioAnulacion);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(InspeccionEstados.EnCurso, 1)]
    [InlineData(InspeccionEstados.Anulada, 1)]
    public async Task MisInspecciones_IncluyeLasAnuladasSalvoQueSeFiltrePorEstado(string? estado, int esperadas)
    {
        var e = await AnuladaAsync();
        await OtraEnCursoAsync(e);

        var action = await Como(e, e.Tecnico).GetMisInspecciones(estado);

        var lista = Assert.IsType<ApiResponse<List<InspeccionDto>>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal(esperadas, lista.Count);
        Assert.Equal(estado != InspeccionEstados.EnCurso,
            lista.Any(i => i.Estado == InspeccionEstados.Anulada && i.MotivoAnulacion == MotivoValido));
    }

    // ------------------------------------------------------------------ Fase 3: concurrencia (RowVersion)

    /// <summary>Otra petición deja la inspección en <paramref name="estado"/> y la base rechaza esta.</summary>
    private static Action OtraPeticionLaDejaEn(Escenario e, string estado) => () =>
    {
        using var otra = NuevoContexto(e.NombreBase);
        otra.Inspecciones.Single(i => i.IdInspeccion == e.Inspeccion.IdInspeccion).Estado = estado;
        otra.SaveChanges();
        throw new DbUpdateConcurrencyException("RowVersion distinto");
    };

    [Fact]
    public async Task Firmar_MientrasOtraPeticionLaAnula_Devuelve409()
    {
        var e = await SeedAsync();
        e.Interceptor.AlGuardar = OtraPeticionLaDejaEn(e, InspeccionEstados.Anulada);

        var action = await Como(e, e.Tecnico).FirmarConPassword(e.Context, e.Inspeccion.IdInspeccion,
            new FirmarInspeccionDto { FirmaPngBase64 = FirmaPngValida });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
    }

    [Fact]
    public async Task Anular_MientrasOtraPeticionLaFirma_Devuelve409()
    {
        var e = await SeedAsync();
        e.Interceptor.AlGuardar = OtraPeticionLaDejaEn(e, InspeccionEstados.Cerrada);

        var action = await AnularAsync(e, e.Tecnico);

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeCerrada, Mensaje(action));
    }

    [Fact]
    public async Task Editar_MientrasOtraPeticionLaAnula_Devuelve409()
    {
        var e = await SeedAsync();
        e.Interceptor.AlGuardar = OtraPeticionLaDejaEn(e, InspeccionEstados.Anulada);

        var action = await Como(e, e.Tecnico).UpdateInspeccion(e.Inspeccion.IdInspeccion,
            new UpdateInspeccionDto { Observaciones = "Cambio" });

        Assert.IsType<ConflictObjectResult>(action.Result);
        Assert.Equal(InmutabilidadInspeccion.MensajeAnulada, Mensaje(action));
    }

    [Fact]
    public void Modelo_RowVersionDeInspeccionEsTokenDeConcurrencia()
    {
        using var context = NuevoContexto(Guid.NewGuid().ToString());

        var propiedad = context.Model.FindEntityType(typeof(Inspeccion))!.FindProperty(nameof(Inspeccion.RowVersion))!;

        Assert.True(propiedad.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, propiedad.ValueGenerated);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore, propiedad.GetBeforeSaveBehavior());
    }
}
