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
/// Fase 4 de hallazgos: GET api/hallazgos con filtros por estado, criticidad, equipo, inspección y
/// fechas, paginado como el resto de listados, y GET api/hallazgos/{id} con su origen.
/// </summary>
public class HallazgosConsultaTests
{
    private sealed class FakeCurrentUser(long idUsuario, params string[] roles) : ICurrentUser
    {
        public long IdUsuario => idUsuario;
        public string Nombre => "Auditor";
        public IReadOnlyCollection<string> Roles => roles;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Escenario(
        ECARDbContext Context,
        Usuario Inspector,
        Usuario Responsable,
        Equipo Balanza,
        Equipo Horno,
        Inspeccion InspeccionBalanza,
        Inspeccion InspeccionHorno,
        PreguntaChecklist Pregunta,
        RespuestaInspeccion Respuesta,
        Hallazgo Novedad,
        Hallazgo EnProceso,
        Hallazgo Cerrado,
        Hallazgo Anulado);

    private static readonly DateTime Dia1 = new(2026, 10, 1, 8, 0, 0);
    private static readonly DateTime Dia2 = new(2026, 10, 2, 23, 30, 0);
    private static readonly DateTime Dia3 = new(2026, 10, 3, 0, 0, 0);
    private static readonly DateTime Dia5 = new(2026, 10, 5, 12, 0, 0);

    private static async Task<Escenario> SeedAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var inspector = new Usuario { Nombre = "Ana Inspectora", Correo = "ana@ecar.test", PasswordHash = "hash", Activo = true };
        var responsable = new Usuario { Nombre = "Luis Responsable", Correo = "luis@ecar.test", PasswordHash = "hash", Activo = true };
        var ubicacion = new Ubicacion { Planta = "Planta 1", Area = "Laboratorio" };
        var balanza = new Equipo { CodigoInterno = "BAL-01", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Criticidad = "Alta", Ubicacion = ubicacion, Activo = true };
        var horno = new Equipo { CodigoInterno = "HOR-01", ActivoFijo = "AF-2", NombreEquipo = "Horno", Criticidad = "Baja", Activo = true };
        var pregunta = new PreguntaChecklist { Pregunta = "¿Está calibrada?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true };
        var checklist = new Checklist { Nombre = "Diario", Version = "1.0", Activo = true, Preguntas = [pregunta] };
        context.AddRange(inspector, responsable, balanza, horno, checklist);
        await context.SaveChangesAsync();

        Inspeccion Nueva(Equipo equipo) => new()
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = inspector.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = Dia1,
            Estado = InspeccionEstados.Cerrada
        };
        var inspeccionBalanza = Nueva(balanza);
        var inspeccionHorno = Nueva(horno);
        context.Inspecciones.AddRange(inspeccionBalanza, inspeccionHorno);
        await context.SaveChangesAsync();

        var respuesta = new RespuestaInspeccion
        {
            IdInspeccion = inspeccionBalanza.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "No",
            Observacion = "Descalibrada 0,5 g"
        };
        context.RespuestasInspeccion.Add(respuesta);
        await context.SaveChangesAsync();

        Hallazgo Hallazgo(Inspeccion inspeccion, string descripcion, string estado, string? criticidad, DateTime fecha) => new()
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Descripcion = descripcion,
            Estado = estado,
            Criticidad = criticidad,
            FechaRegistro = fecha,
            Origen = HallazgoOrigenes.Manual,
            IdUsuarioRegistro = inspector.IdUsuario
        };

        var novedad = Hallazgo(inspeccionBalanza, "¿Está calibrada?: Descalibrada 0,5 g", HallazgoEstados.Abierto, "Alta", Dia1);
        novedad.Origen = HallazgoOrigenes.Novedad;
        novedad.IdPregunta = pregunta.IdPregunta;
        novedad.IdRespuesta = respuesta.IdRespuesta;
        novedad.IdUsuarioResponsable = responsable.IdUsuario;
        novedad.FechaCompromiso = new DateTime(2026, 10, 20);

        var enProceso = Hallazgo(inspeccionBalanza, "Cable suelto", HallazgoEstados.EnProceso, "Media", Dia2);
        var cerrado = Hallazgo(inspeccionHorno, "Puerta descuadrada", HallazgoEstados.Cerrado, "Baja", Dia3);
        cerrado.AccionCorrectiva = "Se ajustaron las bisagras";
        cerrado.FechaCierre = Dia5;
        cerrado.IdUsuarioCierre = responsable.IdUsuario;
        var anulado = Hallazgo(inspeccionHorno, "Registro duplicado", HallazgoEstados.Anulado, null, Dia5);
        anulado.MotivoAnulacion = "Se registró dos veces por error";

        context.Hallazgos.AddRange(novedad, enProceso, cerrado, anulado);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return new Escenario(context, inspector, responsable, balanza, horno, inspeccionBalanza, inspeccionHorno,
            pregunta, respuesta, novedad, enProceso, cerrado, anulado);
    }

    private static HallazgosController Controlador(Escenario escenario) =>
        new(escenario.Context, new FakeCurrentUser(escenario.Inspector.IdUsuario, "Auditor"), new MotivoCambio());

    private static async Task<PagedResultDto<HallazgoDto>> ListarAsync(Escenario escenario, HallazgoFiltroDto filtro,
        int page = 1, int pageSize = 10)
    {
        var action = await Controlador(escenario).GetHallazgos(filtro, page, pageSize);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        return Assert.IsType<ApiResponse<PagedResultDto<HallazgoDto>>>(ok.Value).Data!;
    }

    private static long[] Ids(PagedResultDto<HallazgoDto> pagina) => pagina.Data.Select(h => h.IdHallazgo).ToArray();

    [Fact]
    public async Task Listar_SinFiltros_DevuelveTodosDelMasRecienteAlMasAntiguo()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto());

        Assert.Equal(4, pagina.TotalCount);
        Assert.Equal([e.Anulado.IdHallazgo, e.Cerrado.IdHallazgo, e.EnProceso.IdHallazgo, e.Novedad.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_CadaFilaTraeEquipoAreaYUsuarios()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto());

        var novedad = pagina.Data.Single(h => h.IdHallazgo == e.Novedad.IdHallazgo);
        Assert.Equal(e.Balanza.IdEquipo, novedad.IdEquipo);
        Assert.Equal("Balanza", novedad.NombreEquipo);
        Assert.Equal("BAL-01", novedad.CodigoInterno);
        Assert.Equal("Planta 1 - Laboratorio", novedad.Area);
        Assert.Equal(HallazgoOrigenes.Novedad, novedad.Origen);
        Assert.Equal("Ana Inspectora", novedad.NombreUsuarioRegistro);
        Assert.Equal(e.Responsable.IdUsuario, novedad.IdUsuarioResponsable);
        Assert.Equal("Luis Responsable", novedad.NombreResponsable);
        Assert.Equal(new DateTime(2026, 10, 20), novedad.FechaCompromiso);

        var cerrado = pagina.Data.Single(h => h.IdHallazgo == e.Cerrado.IdHallazgo);
        Assert.Null(cerrado.Area);
        Assert.Equal("Se ajustaron las bisagras", cerrado.AccionCorrectiva);
        Assert.Equal("Luis Responsable", cerrado.NombreUsuarioCierre);
        Assert.Equal("Se registró dos veces por error",
            pagina.Data.Single(h => h.IdHallazgo == e.Anulado.IdHallazgo).MotivoAnulacion);
    }

    [Theory]
    [InlineData(HallazgoEstados.Abierto)]
    [InlineData(HallazgoEstados.EnProceso)]
    [InlineData(HallazgoEstados.Cerrado)]
    [InlineData(HallazgoEstados.Anulado)]
    public async Task Listar_PorEstado(string estado)
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto { Estado = estado });

        Assert.Equal(1, pagina.TotalCount);
        Assert.Equal(estado, pagina.Data.Single().Estado);
    }

    [Fact]
    public async Task Listar_ConUnEstadoQueNoExiste_Devuelve400()
    {
        var e = await SeedAsync();

        var action = await Controlador(e).GetHallazgos(new HallazgoFiltroDto { Estado = "Pendiente" });

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Listar_PorCriticidad()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto { Criticidad = "Media" });

        Assert.Equal([e.EnProceso.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_PorEquipo()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto { IdEquipo = e.Horno.IdEquipo });

        Assert.Equal([e.Anulado.IdHallazgo, e.Cerrado.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_PorInspeccion()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto { IdInspeccion = e.InspeccionBalanza.IdInspeccion });

        Assert.Equal([e.EnProceso.IdHallazgo, e.Novedad.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_PorRangoDeFechas_IncluyeElDiaEnteroDeAmbosExtremos()
    {
        var e = await SeedAsync();

        // Dia2 es a las 23:30 y Dia3 a las 00:00: ambos entran; Dia1 y Dia5 quedan fuera.
        var pagina = await ListarAsync(e, new HallazgoFiltroDto { Desde = Dia2.Date, Hasta = Dia3.Date });

        Assert.Equal([e.Cerrado.IdHallazgo, e.EnProceso.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_SoloDesdeOSoloHasta()
    {
        var e = await SeedAsync();

        Assert.Equal([e.Anulado.IdHallazgo], Ids(await ListarAsync(e, new HallazgoFiltroDto { Desde = Dia5.Date })));
        Assert.Equal([e.Novedad.IdHallazgo], Ids(await ListarAsync(e, new HallazgoFiltroDto { Hasta = Dia1.Date })));
    }

    [Fact]
    public async Task Listar_ConDesdePosteriorAHasta_Devuelve400()
    {
        var e = await SeedAsync();

        var action = await Controlador(e).GetHallazgos(new HallazgoFiltroDto { Desde = Dia5, Hasta = Dia1 });

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Listar_ConFiltrosCombinados()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto
        {
            IdEquipo = e.Horno.IdEquipo,
            Estado = HallazgoEstados.Cerrado,
            Criticidad = "Baja",
            Desde = Dia3.Date
        });

        Assert.Equal([e.Cerrado.IdHallazgo], Ids(pagina));
    }

    [Fact]
    public async Task Listar_BuscaPorCodigoInternoDelEquipo()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new HallazgoFiltroDto { Search = "HOR-01" });

        Assert.Equal(2, pagina.TotalCount);
    }

    [Fact]
    public async Task Listar_Paginado()
    {
        var e = await SeedAsync();

        var segunda = await ListarAsync(e, new HallazgoFiltroDto(), page: 2, pageSize: 3);

        Assert.Equal(4, segunda.TotalCount);
        Assert.Equal(2, segunda.TotalPages);
        Assert.Equal([e.Novedad.IdHallazgo], Ids(segunda));
        Assert.True(segunda.HasPrevious);
        Assert.False(segunda.HasNext);
    }

    [Fact]
    public async Task Listar_AjustaPaginaYTamanoFueraDeRango()
    {
        var e = await SeedAsync();

        var grande = await ListarAsync(e, new HallazgoFiltroDto(), page: 0, pageSize: 100000);
        var cero = await ListarAsync(e, new HallazgoFiltroDto(), page: -3, pageSize: 0);

        Assert.Equal(1, grande.Page);
        Assert.Equal(100, grande.PageSize);
        Assert.Equal(1, cero.PageSize);
        Assert.Single(cero.Data);
    }

    [Fact]
    public async Task Detalle_DeUnaNovedad_TraeLaPreguntaLaRespuestaYLaInspeccion()
    {
        var e = await SeedAsync();

        var action = await Controlador(e).GetHallazgo(e.Novedad.IdHallazgo);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var detalle = Assert.IsType<ApiResponse<HallazgoDetalleDto>>(ok.Value).Data!;
        Assert.Equal(e.Pregunta.IdPregunta, detalle.IdPregunta);
        Assert.Equal("¿Está calibrada?", detalle.Pregunta);
        Assert.Equal(e.Respuesta.IdRespuesta, detalle.IdRespuesta);
        Assert.Equal("Descalibrada 0,5 g", detalle.ObservacionRespuesta);
        Assert.Equal(Dia1, detalle.FechaInspeccion);
        Assert.Equal(InspeccionEstados.Cerrada, detalle.EstadoInspeccion);
        Assert.Equal("Ana Inspectora", detalle.NombreInspector);
        Assert.Equal("Planta 1 - Laboratorio", detalle.Area);
        Assert.Equal("Luis Responsable", detalle.NombreResponsable);
    }

    [Fact]
    public async Task Detalle_DeUnHallazgoManual_NoTienePreguntaNiRespuesta()
    {
        var e = await SeedAsync();

        var action = await Controlador(e).GetHallazgo(e.Cerrado.IdHallazgo);

        var detalle = Assert.IsType<ApiResponse<HallazgoDetalleDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Null(detalle.IdPregunta);
        Assert.Null(detalle.Pregunta);
        Assert.Null(detalle.IdRespuesta);
        Assert.Null(detalle.ObservacionRespuesta);
        Assert.Equal("Ana Inspectora", detalle.NombreInspector);
    }

    [Fact]
    public async Task Detalle_Inexistente_Devuelve404()
    {
        var e = await SeedAsync();

        var action = await Controlador(e).GetHallazgo(999);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }
}
