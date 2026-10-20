using ECAR.API.Controllers;
using ECAR.Infrastructure.Data;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FilaAuditoria = ECAR.Infrastructure.Entities.Auditoria;

namespace ECAR.API.Tests;

/// <summary>
/// Fase 4 de auditoría: GET api/auditoria con filtros por texto, tabla, registro, usuario, acción
/// y rango de días (locales, convertidos a UTC), paginado como el resto de listados, y el DTO completo.
/// </summary>
public class AuditoriaConsultaTests
{
    /// <summary>Un instante en hora local del servidor, guardado en UTC como hace la auditoría.</summary>
    private static DateTime Local(int dia, int hora, int minuto = 0) =>
        DateTime.SpecifyKind(new DateTime(2026, 10, dia, hora, minuto, 0), DateTimeKind.Local).ToUniversalTime();

    private static FilaAuditoria Fila(string tabla, long registroId, string accion, long? idUsuario, DateTime fecha,
        string usuario = "Ana (ana@ecar.test)") => new()
        {
            Tabla = tabla,
            RegistroId = registroId,
            Accion = accion,
            IdUsuario = idUsuario,
            Usuario = usuario,
            FechaHora = fecha,
            Hash = new string('A', 64)
        };

    private sealed record Escenario(ECARDbContext Context, FilaAuditoria CrearInspeccion, FilaAuditoria AnularInspeccion,
        FilaAuditoria ResolverNovedad, FilaAuditoria Login, FilaAuditoria ModificarEquipo);

    private static async Task<Escenario> SeedAsync()
    {
        var context = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var crear = Fila("Inspecciones", 10, AuditoriaAcciones.Crear, 1, Local(1, 8));
        var anular = Fila("Inspecciones", 10, AuditoriaAcciones.Anular, 2, Local(2, 23, 30), "Luis (luis@ecar.test)");
        anular.ValorAnterior = "{\"Estado\":\"EnCurso\"}";
        anular.ValorNuevo = "{\"Estado\":\"Anulada\"}";
        anular.Motivo = "Se inició sobre el equipo equivocado";
        anular.DireccionIp = "10.0.0.7";
        anular.AgenteUsuario = "Mozilla/5.0";
        anular.HashAnterior = new string('A', 64);
        anular.Hash = new string('B', 64);
        var cerrar = Fila("Novedades", 5, AuditoriaAcciones.Resolver, 2, Local(3, 0), "Luis (luis@ecar.test)");
        var login = Fila(AuditoriaTablas.Sistema, 1, AuditoriaAcciones.LoginExitoso, 1, Local(5, 12));
        var equipo = Fila("Equipos", 7, AuditoriaAcciones.Modificar, null, Local(5, 13), "Sistema");

        context.Auditoria.AddRange(crear, anular, cerrar, login, equipo);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new Escenario(context, crear, anular, cerrar, login, equipo);
    }

    private static async Task<PagedResultDto<AuditoriaDto>> ListarAsync(Escenario e, AuditoriaFiltroDto filtro,
        int page = 1, int pageSize = 10)
    {
        var action = await new AuditoriaController(e.Context).GetAuditoria(filtro, page, pageSize);
        return Assert.IsType<ApiResponse<PagedResultDto<AuditoriaDto>>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
    }

    private static long[] Ids(PagedResultDto<AuditoriaDto> pagina) => pagina.Data.Select(a => a.IdAuditoria).ToArray();

    private static long[] Ids(params FilaAuditoria[] filas) => filas.Select(f => f.IdAuditoria).ToArray();

    [Fact]
    public async Task SinFiltros_DevuelveTodasDeLaMasRecienteALaMasAntigua()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new AuditoriaFiltroDto());

        Assert.Equal(5, pagina.TotalCount);
        Assert.Equal(Ids(e.ModificarEquipo, e.Login, e.ResolverNovedad, e.AnularInspeccion, e.CrearInspeccion), Ids(pagina));
    }

    [Fact]
    public async Task CadaFilaTraeTodosSusCampos()
    {
        var e = await SeedAsync();

        var fila = (await ListarAsync(e, new AuditoriaFiltroDto { Accion = AuditoriaAcciones.Anular })).Data.Single();

        Assert.Equal("Inspecciones", fila.Tabla);
        Assert.Equal(10, fila.RegistroId);
        Assert.Equal(2, fila.IdUsuario);
        Assert.Equal("Luis (luis@ecar.test)", fila.Usuario);
        Assert.Equal("{\"Estado\":\"EnCurso\"}", fila.ValorAnterior);
        Assert.Equal("{\"Estado\":\"Anulada\"}", fila.ValorNuevo);
        Assert.Equal("Se inició sobre el equipo equivocado", fila.Motivo);
        Assert.Equal("10.0.0.7", fila.DireccionIp);
        Assert.Equal("Mozilla/5.0", fila.AgenteUsuario);
        Assert.Equal(new string('B', 64), fila.Hash);
        Assert.Equal(e.AnularInspeccion.FechaHora, fila.FechaHora);
    }

    [Fact]
    public async Task PorTabla()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new AuditoriaFiltroDto { Tabla = "Inspecciones" });

        Assert.Equal(Ids(e.AnularInspeccion, e.CrearInspeccion), Ids(pagina));
    }

    [Fact]
    public async Task PorTablaYRegistro()
    {
        var e = await SeedAsync();

        Assert.Equal(Ids(e.ResolverNovedad), Ids(await ListarAsync(e, new AuditoriaFiltroDto { Tabla = "Novedades", RegistroId = 5 })));
        Assert.Empty((await ListarAsync(e, new AuditoriaFiltroDto { Tabla = "Novedades", RegistroId = 10 })).Data);
    }

    [Fact]
    public async Task PorUsuario()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new AuditoriaFiltroDto { IdUsuario = 2 });

        Assert.Equal(Ids(e.ResolverNovedad, e.AnularInspeccion), Ids(pagina));
    }

    [Fact]
    public async Task PorAccion()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new AuditoriaFiltroDto { Accion = AuditoriaAcciones.LoginExitoso });

        Assert.Equal(Ids(e.Login), Ids(pagina));
    }

    [Fact]
    public async Task PorRangoDeDias_IncluyeElDiaEnteroDeAmbosExtremos()
    {
        var e = await SeedAsync();

        // El día 2 a las 23:30 y el día 3 a las 00:00 (hora local) entran; el 1 y el 5 no.
        var pagina = await ListarAsync(e, new AuditoriaFiltroDto
        {
            Desde = new DateTime(2026, 10, 2),
            Hasta = new DateTime(2026, 10, 3)
        });

        Assert.Equal(Ids(e.ResolverNovedad, e.AnularInspeccion), Ids(pagina));
    }

    [Fact]
    public async Task SoloDesdeOSoloHasta()
    {
        var e = await SeedAsync();

        Assert.Equal(Ids(e.ModificarEquipo, e.Login),
            Ids(await ListarAsync(e, new AuditoriaFiltroDto { Desde = new DateTime(2026, 10, 5) })));
        Assert.Equal(Ids(e.CrearInspeccion),
            Ids(await ListarAsync(e, new AuditoriaFiltroDto { Hasta = new DateTime(2026, 10, 1) })));
    }

    [Fact]
    public async Task ConFiltrosCombinados()
    {
        var e = await SeedAsync();

        var pagina = await ListarAsync(e, new AuditoriaFiltroDto
        {
            Tabla = "Inspecciones",
            IdUsuario = 1,
            Desde = new DateTime(2026, 10, 1),
            Hasta = new DateTime(2026, 10, 31)
        });

        Assert.Equal(Ids(e.CrearInspeccion), Ids(pagina));
    }

    [Fact]
    public async Task ElTextoLibreSigueBuscandoEnTablaAccionYUsuario()
    {
        var e = await SeedAsync();

        Assert.Equal(Ids(e.Login), Ids(await ListarAsync(e, new AuditoriaFiltroDto { Search = "Login" })));
        Assert.Equal(2, (await ListarAsync(e, new AuditoriaFiltroDto { Search = "luis@" })).TotalCount);
    }

    [Theory]
    [InlineData("Borrados", null)]
    [InlineData(null, "Destruir")]
    public async Task ConTablaOAccionQueNoExisten_Devuelve400(string? tabla, string? accion)
    {
        var e = await SeedAsync();

        var action = await new AuditoriaController(e.Context).GetAuditoria(new AuditoriaFiltroDto { Tabla = tabla, Accion = accion });

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task ConDesdePosteriorAHasta_Devuelve400()
    {
        var e = await SeedAsync();

        var action = await new AuditoriaController(e.Context).GetAuditoria(new AuditoriaFiltroDto
        {
            Desde = new DateTime(2026, 10, 5),
            Hasta = new DateTime(2026, 10, 1)
        });

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Paginado()
    {
        var e = await SeedAsync();

        var segunda = await ListarAsync(e, new AuditoriaFiltroDto(), page: 2, pageSize: 2);

        Assert.Equal(5, segunda.TotalCount);
        Assert.Equal(3, segunda.TotalPages);
        Assert.Equal(Ids(e.ResolverNovedad, e.AnularInspeccion), Ids(segunda));
        Assert.True(segunda.HasPrevious);
        Assert.True(segunda.HasNext);
    }

    [Fact]
    public async Task AjustaPaginaYTamanoFueraDeRango()
    {
        var e = await SeedAsync();

        var grande = await ListarAsync(e, new AuditoriaFiltroDto(), page: 0, pageSize: 5000);
        var cero = await ListarAsync(e, new AuditoriaFiltroDto(), page: -1, pageSize: 0);

        Assert.Equal(1, grande.Page);
        Assert.Equal(100, grande.PageSize);
        Assert.Single(cero.Data);
    }

    [Fact]
    public async Task Detalle_TraeTodosLosCampos()
    {
        var e = await SeedAsync();

        var action = await new AuditoriaController(e.Context).GetAuditoriaRegistro(e.AnularInspeccion.IdAuditoria);

        var fila = Assert.IsType<ApiResponse<AuditoriaDto>>(Assert.IsType<OkObjectResult>(action.Result).Value).Data!;
        Assert.Equal("Se inició sobre el equipo equivocado", fila.Motivo);
        Assert.Equal("10.0.0.7", fila.DireccionIp);
        Assert.Equal(new string('B', 64), fila.Hash);
    }

    [Fact]
    public async Task Detalle_Inexistente_Devuelve404()
    {
        var e = await SeedAsync();

        var action = await new AuditoriaController(e.Context).GetAuditoriaRegistro(999);

        Assert.IsType<NotFoundObjectResult>(action.Result);
    }
}
