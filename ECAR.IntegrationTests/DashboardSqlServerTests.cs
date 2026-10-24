using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// B-08 (ECAR-223): filtros del tablero (B-01, GET api/dashboard?desde&amp;hasta&amp;planta) contra
/// SQL Server. Las fechas del periodo son días de Colombia con los dos extremos incluidos, y la
/// planta filtra equipos, inspecciones y novedades. Cada prueba usa su propia planta (ver
/// <see cref="DatosTablero"/>) porque la base es compartida.
/// </summary>
public class DashboardSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private static readonly DateTime Desde = new(2025, 3, 1);
    private static readonly DateTime Hasta = new(2025, 3, 31);

    private BaseDePruebas Base => compartida.Base;

    private static async Task<DashboardDto> TableroAsync(BaseDePruebas baseDePruebas,
        DateTime? desde = null, DateTime? hasta = null, string? planta = null)
    {
        await using var db = baseDePruebas.CrearContexto();
        var resultado = await new DashboardController(db).GetDashboard(desde, hasta, planta);
        return Assert.IsType<ApiResponse<DashboardDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
    }

    [Fact]
    public async Task La_planta_y_el_periodo_en_hora_de_Colombia_filtran_equipos_inspecciones_y_novedades()
    {
        var datos = new DatosTablero(Base);
        var otra = new DatosTablero(Base);
        var alta = await datos.EquipoAsync("Área A", Criticidades.Alta);
        await datos.EquipoAsync("Área B", Criticidades.Media);
        await datos.EquipoAsync("Área A", null);
        await datos.EquipoAsync("Área A", Criticidades.Baja, activo: false);
        var deOtra = await otra.EquipoAsync("Área A", Criticidades.Alta);

        var primerDia = DatosTablero.Utc(Desde);                         // 01/03 00:00 en Colombia
        var ultimaHora = DatosTablero.Utc(Hasta.AddHours(23).AddMinutes(30)); // 31/03 23:30 = 01/04 04:30 UTC
        var diaSiguiente = DatosTablero.Utc(Hasta.AddDays(1));           // 01/04 00:00 en Colombia
        var diaAnterior = DatosTablero.Utc(Desde.AddMinutes(-1));        // 28/02 23:59 en Colombia

        await datos.InspeccionAsync(alta, InspeccionEstados.Cerrada, primerDia, primerDia);
        var conNovedades = await datos.InspeccionAsync(alta, InspeccionEstados.NovedadesPendientes, ultimaHora, ultimaHora);
        await datos.InspeccionAsync(alta, InspeccionEstados.Cerrada, diaSiguiente, diaSiguiente);
        await datos.InspeccionAsync(alta, InspeccionEstados.Cerrada, diaAnterior, diaAnterior);
        await datos.InspeccionAsync(alta, InspeccionEstados.EnCurso, primerDia.AddDays(5));
        await datos.InspeccionAsync(alta, InspeccionEstados.Anulada, primerDia.AddDays(6));
        var deOtraPlanta = await otra.InspeccionAsync(deOtra, InspeccionEstados.NovedadesPendientes, primerDia, primerDia);

        await datos.NovedadAsync(conNovedades, NovedadEstados.Pendiente, ultimaHora);
        await datos.NovedadAsync(conNovedades, NovedadEstados.Pendiente, diaSiguiente);
        await datos.NovedadAsync(conNovedades, NovedadEstados.Resuelta, diaAnterior, primerDia);
        await datos.NovedadAsync(conNovedades, NovedadEstados.Resuelta, primerDia, diaSiguiente);
        await otra.NovedadAsync(deOtraPlanta, NovedadEstados.Pendiente, primerDia);

        var tablero = await TableroAsync(Base, Desde, Hasta, "  " + datos.Planta + "  ");

        Assert.Equal(Desde, tablero.Filtros.Desde);
        Assert.Equal(Hasta, tablero.Filtros.Hasta);
        Assert.Equal(datos.Planta, tablero.Filtros.Planta);
        Assert.Equal("marzo de 2025", tablero.Periodo);

        // Equipos: solo los activos de la planta.
        Assert.Equal(3, tablero.TotalEquipos);
        Assert.Equal(
            new[] { (Criticidades.Alta, 1), (Criticidades.Media, 1), (Criticidades.Baja, 0), ("Sin criticidad", 1) },
            tablero.EquiposPorCriticidad.Select(c => (c.Etiqueta, c.Cantidad)));
        Assert.Equal(
            new[] { ($"{datos.Planta} - Área A", 2), ($"{datos.Planta} - Área B", 1) },
            tablero.EquiposPorArea.Select(c => (c.Etiqueta, c.Cantidad)));

        // Inspecciones del periodo (por FechaInspeccion): el primer día a las 00:00 y el último a las
        // 23:30 cuentan; el día siguiente a las 00:00 y el anterior a las 23:59, no.
        Assert.Equal(2, tablero.InspeccionesRealizadasMes);
        Assert.Equal(1, tablero.InspeccionesConNovedadesPendientes);
        Assert.Equal(1, tablero.InspeccionesEnCurso);
        // Las firmadas desde el inicio ignoran el periodo, pero no la planta.
        Assert.Equal(4, tablero.InspeccionesRealizadas);

        // Novedades: pendientes por FechaRegistro y resueltas por FechaResolucion.
        Assert.Equal(1, tablero.NovedadesPendientes);
        Assert.Equal(1, tablero.NovedadesResueltas);
    }

    [Fact]
    public async Task El_filtro_de_planta_sigue_la_collation_de_SQL_Server()
    {
        var datos = new DatosTablero(Base);
        await datos.EquipoAsync("Área A", Criticidades.Alta);
        var collation = await Base.EscalarAsync<string>("SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'))");

        var tablero = await TableroAsync(Base, Desde, Hasta, datos.Planta.ToUpperInvariant());

        // La planta se compara con == y lo decide SQL Server: con la collation por defecto (_CI_)
        // no distingue mayúsculas. El proveedor en memoria sí las distingue, por eso se prueba aquí.
        var esperado = collation.Contains("_CI_", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        Assert.Equal(esperado, tablero.TotalEquipos);
        Assert.Equal(datos.Planta.ToUpperInvariant(), tablero.Filtros.Planta);
    }

    [Theory]
    [InlineData("2025-03-31", "2025-03-01", "La fecha «Desde» no puede ser posterior a «Hasta»")]
    [InlineData("2025-01-01", "2026-01-02", "El rango no puede superar un año")]
    public async Task Un_rango_invalido_devuelve_400(string desde, string hasta, string mensaje)
    {
        await using var db = Base.CrearContexto();

        var resultado = await new DashboardController(db).GetDashboard(DateTime.Parse(desde), DateTime.Parse(hasta));

        var respuesta = Assert.IsType<ApiResponse<DashboardDto>>(Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        Assert.Equal(mensaje, respuesta.Message);
    }

    [Fact]
    public async Task Sin_filtros_toma_el_mes_en_curso_y_todas_las_plantas()
    {
        // Base propia: sin planta, el tablero cuenta todo lo que hay en la base.
        await using var propia = await BaseDePruebas.CrearAsync();
        var norte = new DatosTablero(propia);
        var sur = new DatosTablero(propia);
        var equipoNorte = await norte.EquipoAsync("Área A", Criticidades.Alta);
        var equipoSur = await sur.EquipoAsync("Área B", Criticidades.Baja);
        var ahora = DateTime.UtcNow;
        await norte.InspeccionAsync(equipoNorte, InspeccionEstados.Cerrada, ahora, ahora);
        await sur.InspeccionAsync(equipoSur, InspeccionEstados.NovedadesPendientes, ahora, ahora);

        var tablero = await TableroAsync(propia);

        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        Assert.Equal(new DateTime(hoy.Year, hoy.Month, 1), tablero.Filtros.Desde);
        Assert.Equal(hoy, tablero.Filtros.Hasta);
        Assert.Null(tablero.Filtros.Planta);
        Assert.Equal(2, tablero.TotalEquipos);
        Assert.Equal(2, tablero.InspeccionesRealizadasMes);
        Assert.Equal(2, tablero.InspeccionesRealizadas);
        Assert.Equal(1, tablero.InspeccionesConNovedadesPendientes);
    }
}
