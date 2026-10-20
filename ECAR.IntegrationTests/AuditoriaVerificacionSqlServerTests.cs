using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Verificación de la cadena de auditoría contra SQL Server, con filas escritas por la auditoría
/// transaccional real. Para alterarlas hay que desactivar el trigger de solo inserción, cosa que
/// solo se hace aquí, en una base temporal propia de cada prueba (la cadena queda rota a propósito).
/// </summary>
public class AuditoriaVerificacionSqlServerTests
{
    private const string Trigger = ECARDbContext.TriggerAuditoriaSoloInsercion;

    /// <summary>Tres equipos creados en guardados distintos: tres filas "Crear" encadenadas.</summary>
    private static async Task<List<long>> FilasDeAuditoriaAsync(BaseDePruebas baseDePruebas)
    {
        for (var i = 0; i < 3; i++)
        {
            await using var db = baseDePruebas.CrearContexto(new ContextoDePrueba { Motivo = $"Alta {i}" });
            var codigo = "IT-" + Guid.NewGuid().ToString("N")[..10];
            db.Equipos.Add(new Equipo { CodigoInterno = codigo, ActivoFijo = "AF-" + codigo, NombreEquipo = "Equipo " + codigo });
            await db.SaveChangesAsync();
        }

        await using var lectura = baseDePruebas.CrearContexto();
        return await lectura.Auditoria.OrderBy(a => a.IdAuditoria).Select(a => a.IdAuditoria).ToListAsync();
    }

    /// <summary>Lo que haría alguien con acceso directo a la base: apagar el trigger, cambiar y volver a encenderlo.</summary>
    private static Task SaltandoseElTriggerAsync(BaseDePruebas baseDePruebas, string sql) =>
        baseDePruebas.EjecutarAsync(
            $"DISABLE TRIGGER [dbo].[{Trigger}] ON [dbo].[Auditoria]; {sql}; ENABLE TRIGGER [dbo].[{Trigger}] ON [dbo].[Auditoria];");

    private static async Task<ResultadoCadena> VerificarAsync(BaseDePruebas baseDePruebas)
    {
        await using var db = baseDePruebas.CrearContexto();
        return await CadenaAuditoria.VerificarTablaAsync(db, tamanoLote: 2);
    }

    [Fact]
    public async Task La_cadena_escrita_por_la_auditoria_real_es_valida()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync();
        var filas = await FilasDeAuditoriaAsync(baseDePruebas);

        var resultado = await VerificarAsync(baseDePruebas);

        Assert.True(resultado.Valida);
        Assert.Equal(filas.Count, resultado.FilasRevisadas);
    }

    [Fact]
    public async Task Sin_desactivar_el_trigger_no_se_puede_alterar_ninguna_fila()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync();
        var filas = await FilasDeAuditoriaAsync(baseDePruebas);

        await Assert.ThrowsAsync<SqlException>(() =>
            baseDePruebas.EjecutarAsync($"UPDATE Auditoria SET Motivo = N'alterado' WHERE IdAuditoria = {filas[1]}"));

        Assert.True((await VerificarAsync(baseDePruebas)).Valida);
    }

    [Fact]
    public async Task Una_fila_alterada_por_SQL_se_detecta_y_se_indica_cual()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync();
        var filas = await FilasDeAuditoriaAsync(baseDePruebas);

        await SaltandoseElTriggerAsync(baseDePruebas,
            $"UPDATE Auditoria SET ValorNuevo = N'{{\"NombreEquipo\":\"Otro\"}}' WHERE IdAuditoria = {filas[1]}");
        var resultado = await VerificarAsync(baseDePruebas);

        Assert.False(resultado.Valida);
        Assert.Equal(filas[1], resultado.PrimeraFilaRota);
        Assert.Equal(2, resultado.FilasRevisadas);
    }

    [Fact]
    public async Task Un_truncado_por_SQL_lo_detecta_el_ancla_externa()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync();
        var filas = await FilasDeAuditoriaAsync(baseDePruebas);
        var ancla = await VerificarAsync(baseDePruebas);

        await SaltandoseElTriggerAsync(baseDePruebas, $"DELETE FROM Auditoria WHERE IdAuditoria >= {filas[1]}");
        await using var db = baseDePruebas.CrearContexto();

        Assert.True((await VerificarAsync(baseDePruebas)).Valida);
        Assert.Equal(filas[^1], ancla.UltimaFila);
        Assert.False(await CadenaAuditoria.AnclaIntactaAsync(db, ancla.UltimaFila!.Value, ancla.UltimoHash!));
    }

    [Fact]
    public async Task Una_fila_borrada_por_SQL_la_delata_la_siguiente()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync();
        var filas = await FilasDeAuditoriaAsync(baseDePruebas);

        await SaltandoseElTriggerAsync(baseDePruebas, $"DELETE FROM Auditoria WHERE IdAuditoria = {filas[1]}");
        var resultado = await VerificarAsync(baseDePruebas);

        Assert.False(resultado.Valida);
        Assert.Equal(filas[2], resultado.PrimeraFilaRota);
    }
}
