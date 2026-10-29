using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// B-08 (ECAR-223): tendencia semanal del tablero (B-02, GET api/dashboard/tendencia) contra SQL
/// Server. Las semanas van de lunes a domingo en hora de Colombia y terminan en la actual; las
/// inspecciones cuentan por FechaCierre y las novedades por FechaRegistro y FechaResolucion.
/// </summary>
/// <remarks>
/// La tendencia toma la fecha de DateTime.UtcNow, así que los datos se siembran relativos al lunes
/// de la semana actual y nunca después de ahora.
/// </remarks>
public class DashboardTendenciaSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private BaseDePruebas Base => compartida.Base;

    /// <summary>Lunes de la semana actual en Colombia, con la misma regla del controlador.</summary>
    private static DateTime LunesActual()
    {
        var hoy = HoraColombia.DesdeUtc(DateTime.UtcNow).Date;
        return hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
    }

    [Fact]
    public async Task Cuenta_por_semana_colombiana_con_semanas_vacias_y_filtro_de_planta()
    {
        var datos = new DatosTablero(Base);
        var otra = new DatosTablero(Base);
        var equipo = await datos.EquipoAsync("Área A", Criticidades.Alta);
        var equipoOtra = await otra.EquipoAsync("Área A", Criticidades.Alta);
        var lunes = LunesActual();
        var semana0 = lunes.AddDays(-21);
        var semana1 = lunes.AddDays(-14);
        var semana2 = lunes.AddDays(-7);

        // Domingo de la semana 1 a las 23:30 en Colombia (lunes 04:30 UTC): sigue en la semana 1.
        var domingoNoche = DatosTablero.Utc(semana2.AddMinutes(-30));
        var firmada = await datos.InspeccionAsync(equipo, InspeccionEstados.Cerrada, domingoNoche, domingoNoche);
        // Lunes de la semana 2 a las 00:00 en Colombia: ya es la semana 2.
        var lunesSemana2 = DatosTablero.Utc(semana2);
        await datos.InspeccionAsync(equipo, InspeccionEstados.NovedadesPendientes, lunesSemana2, lunesSemana2);
        // No cuentan: en curso, anulada aunque tenga cierre, cerrada antes del rango y la de otra planta.
        await datos.InspeccionAsync(equipo, InspeccionEstados.EnCurso, DatosTablero.Utc(semana2.AddDays(2)));
        await datos.InspeccionAsync(equipo, InspeccionEstados.Anulada, DatosTablero.Utc(semana2.AddDays(2)), DatosTablero.Utc(semana2.AddDays(2)));
        await datos.InspeccionAsync(equipo, InspeccionEstados.Cerrada, DatosTablero.Utc(semana0.AddDays(-1)), DatosTablero.Utc(semana0.AddDays(-1)));
        var deOtra = await otra.InspeccionAsync(equipoOtra, InspeccionEstados.Cerrada, lunesSemana2, lunesSemana2);

        // Registrada en la semana 0; registrada antes del rango y resuelta en la semana 1; resuelta
        // el lunes actual a las 00:00 en Colombia.
        await datos.NovedadAsync(firmada, NovedadEstados.Pendiente, DatosTablero.Utc(semana0.AddDays(1)));
        await datos.NovedadAsync(firmada, NovedadEstados.Resuelta, DatosTablero.Utc(semana0.AddDays(-30)), DatosTablero.Utc(semana1.AddDays(3)));
        await datos.NovedadAsync(firmada, NovedadEstados.Resuelta, DatosTablero.Utc(semana0.AddDays(-30)), DatosTablero.Utc(lunes));
        await otra.NovedadAsync(deOtra, NovedadEstados.Resuelta, lunesSemana2, lunesSemana2);

        await using var db = Base.CrearContexto();
        var resultado = await new DashboardController(db).GetTendencia(4, [" " + datos.Planta + " "]);

        var tendencia = Assert.IsType<ApiResponse<DashboardTendenciaDto>>(Assert.IsType<OkObjectResult>(resultado.Result).Value).Data!;
        Assert.Equal(4, tendencia.Semanas);
        Assert.Equal([datos.Planta], tendencia.Plantas);
        Assert.Equal(semana0, tendencia.Desde);
        Assert.Equal(lunes.AddDays(6), tendencia.Hasta);
        Assert.Equal(new[] { semana0, semana1, semana2, lunes }, tendencia.Datos.Select(s => s.Desde));
        Assert.Equal(new[] { semana0.AddDays(6), semana1.AddDays(6), semana2.AddDays(6), lunes.AddDays(6) },
            tendencia.Datos.Select(s => s.Hasta));
        Assert.Equal(new[] { 0, 1, 1, 0 }, tendencia.Datos.Select(s => s.InspeccionesFirmadas));
        Assert.Equal(new[] { 1, 0, 0, 0 }, tendencia.Datos.Select(s => s.NovedadesRegistradas));
        Assert.Equal(new[] { 0, 1, 0, 1 }, tendencia.Datos.Select(s => s.NovedadesResueltas));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public async Task Fuera_de_1_a_52_semanas_devuelve_400(int semanas)
    {
        await using var db = Base.CrearContexto();

        var resultado = await new DashboardController(db).GetTendencia(semanas);

        var respuesta = Assert.IsType<ApiResponse<DashboardTendenciaDto>>(Assert.IsType<BadRequestObjectResult>(resultado.Result).Value);
        Assert.Equal("La cantidad de semanas debe estar entre 1 y 52", respuesta.Message);
    }
}
