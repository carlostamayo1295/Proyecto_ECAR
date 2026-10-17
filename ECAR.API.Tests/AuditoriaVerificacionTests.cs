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
/// Fase 6 de auditoría: GET api/auditoria/verificar recorre la cadena de hashes por lotes y
/// detecta una fila alterada o borrada, indicando la primera inconsistente. En memoria se puede
/// modificar una fila; contra SQL Server lo impide el trigger (ver AuditoriaVerificacionSqlServerTests).
/// </summary>
public class AuditoriaVerificacionTests
{
    private static readonly DateTime Dia1 = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private static ECARDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Escribe filas encadenadas como lo hace ECARDbContext, una por guardado, con las fechas dadas.</summary>
    private static async Task<List<FilaAuditoria>> CadenaAsync(ECARDbContext db, params DateTime[] fechas)
    {
        var anterior = await db.Auditoria.OrderByDescending(a => a.IdAuditoria).Select(a => a.Hash).FirstOrDefaultAsync();
        var filas = new List<FilaAuditoria>();
        for (var i = 0; i < fechas.Length; i++)
        {
            var fila = new FilaAuditoria
            {
                Tabla = "Equipos",
                RegistroId = i + 1,
                Accion = AuditoriaAcciones.Modificar,
                ValorAnterior = $"{{\"Nombre\":\"Antes {i}\"}}",
                ValorNuevo = $"{{\"Nombre\":\"Después {i}\"}}",
                Usuario = "Ana (ana@ecar.test)",
                IdUsuario = 1,
                FechaHora = fechas[i],
                HashAnterior = anterior
            };
            fila.Hash = CadenaAuditoria.CalcularHash(fila);
            db.Auditoria.Add(fila);
            await db.SaveChangesAsync();
            anterior = fila.Hash;
            filas.Add(fila);
        }

        db.ChangeTracker.Clear();
        return filas;
    }

    private static DateTime[] Horas(int cantidad) => Enumerable.Range(0, cantidad).Select(i => Dia1.AddMinutes(i)).ToArray();

    /// <summary>Modifica una fila saltándose la aplicación, como haría alguien con acceso a la base.</summary>
    private static async Task AlterarAsync(ECARDbContext db, long idAuditoria, Action<FilaAuditoria> cambio)
    {
        var fila = await db.Auditoria.SingleAsync(a => a.IdAuditoria == idAuditoria);
        cambio(fila);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private static async Task<(VerificacionAuditoriaDto Dto, string Mensaje)> VerificarAsync(ECARDbContext db,
        DateTime? desde = null, DateTime? hasta = null)
    {
        var action = await new AuditoriaController(db).VerificarCadena(desde, hasta);
        var respuesta = Assert.IsType<ApiResponse<VerificacionAuditoriaDto>>(Assert.IsType<OkObjectResult>(action.Result).Value);
        return (respuesta.Data!, respuesta.Message);
    }

    // ------------------------------------------------------------------ tabla entera

    [Fact]
    public async Task UnaCadenaIntacta_EsValida()
    {
        await using var db = NuevoContexto();
        await CadenaAsync(db, Horas(5));

        var (dto, mensaje) = await VerificarAsync(db);

        Assert.True(dto.Valida);
        Assert.Equal(5, dto.FilasRevisadas);
        Assert.Null(dto.PrimeraFilaRota);
        Assert.True(dto.VerificadoEn > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal("La cadena de auditoría está íntegra", mensaje);
    }

    [Fact]
    public async Task UnaTablaVacia_EsValida()
    {
        await using var db = NuevoContexto();

        var resultado = await CadenaAuditoria.VerificarTablaAsync(db);

        Assert.True(resultado.Valida);
        Assert.Equal(0, resultado.FilasRevisadas);
    }

    public static TheoryData<string, Action<FilaAuditoria>> Alteraciones() => new()
    {
        { "ValorAnterior", f => f.ValorAnterior = "{\"Nombre\":\"Otro\"}" },
        { "ValorNuevo", f => f.ValorNuevo = "{\"Nombre\":\"Otro\"}" },
        { "Accion", f => f.Accion = AuditoriaAcciones.Crear },
        { "Tabla", f => f.Tabla = "Usuarios" },
        { "RegistroId", f => f.RegistroId = 99 },
        { "IdUsuario", f => f.IdUsuario = 2 },
        { "FechaHora", f => f.FechaHora = f.FechaHora.AddSeconds(1) },
        { "Motivo", f => f.Motivo = "Añadido después" }
    };

    [Theory]
    [MemberData(nameof(Alteraciones))]
    public async Task UnaFilaAlterada_SeDetectaYSeIndicaCual(string campo, Action<FilaAuditoria> cambio)
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(5));
        await AlterarAsync(db, filas[2].IdAuditoria, cambio);

        var (dto, mensaje) = await VerificarAsync(db);

        Assert.False(dto.Valida, campo);
        Assert.Equal(filas[2].IdAuditoria, dto.PrimeraFilaRota);
        Assert.Equal(3, dto.FilasRevisadas);
        Assert.Contains(filas[2].IdAuditoria.ToString(), mensaje);
    }

    [Fact]
    public async Task UnaFilaAlteradaConSuHashRecalculado_LaDelataLaSiguiente()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(5));
        await AlterarAsync(db, filas[2].IdAuditoria, f =>
        {
            f.ValorNuevo = "{\"Nombre\":\"Otro\"}";
            f.Hash = CadenaAuditoria.CalcularHash(f);
        });

        var (dto, _) = await VerificarAsync(db);

        Assert.False(dto.Valida);
        Assert.Equal(filas[3].IdAuditoria, dto.PrimeraFilaRota);
    }

    [Fact]
    public async Task UnaFilaBorrada_LaDelataLaSiguiente()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(5));
        db.Auditoria.Remove(await db.Auditoria.SingleAsync(a => a.IdAuditoria == filas[1].IdAuditoria));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (dto, _) = await VerificarAsync(db);

        Assert.False(dto.Valida);
        Assert.Equal(filas[2].IdAuditoria, dto.PrimeraFilaRota);
    }

    [Fact]
    public async Task LaPrimeraFilaConHashAnterior_SeDetecta()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(3));
        await AlterarAsync(db, filas[0].IdAuditoria, f => f.HashAnterior = new string('F', 64));

        var (dto, _) = await VerificarAsync(db);

        Assert.Equal(filas[0].IdAuditoria, dto.PrimeraFilaRota);
    }

    [Fact]
    public async Task PorLotes_RevisaTodasLasFilasYSeDetieneEnLaRota()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(7));

        var intacta = await CadenaAuditoria.VerificarTablaAsync(db, tamanoLote: 2);
        await AlterarAsync(db, filas[5].IdAuditoria, f => f.Motivo = "Añadido después");
        var rota = await CadenaAuditoria.VerificarTablaAsync(db, tamanoLote: 2);

        Assert.True(intacta.Valida);
        Assert.Equal(7, intacta.FilasRevisadas);
        Assert.Equal(filas[6].Hash, intacta.UltimoHash);
        Assert.False(rota.Valida);
        Assert.Equal(filas[5].IdAuditoria, rota.PrimeraFilaRota);
        Assert.Equal(6, rota.FilasRevisadas);
    }

    // ------------------------------------------------------------------ por tramos

    [Fact]
    public async Task UnTramo_SoloRevisaSusFilasYLasAnclaALaAnterior()
    {
        await using var db = NuevoContexto();
        await CadenaAsync(db, Dia1, Dia1.AddDays(1), Dia1.AddDays(1).AddHours(5), Dia1.AddDays(2), Dia1.AddDays(4));

        var tramo = await CadenaAuditoria.VerificarTablaAsync(db, Dia1.AddDays(1).Date, Dia1.AddDays(3).Date);

        Assert.True(tramo.Valida);
        Assert.Equal(3, tramo.FilasRevisadas);
    }

    [Fact]
    public async Task UnaAlteracionFueraDelTramo_NoAfectaAlTramoPeroSiALaTablaEntera()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Dia1, Dia1.AddDays(1), Dia1.AddDays(2), Dia1.AddDays(4));
        await AlterarAsync(db, filas[3].IdAuditoria, f => f.ValorNuevo = "{}");

        var tramo = await CadenaAuditoria.VerificarTablaAsync(db, Dia1.AddDays(1).Date, Dia1.AddDays(3).Date);
        var completa = await CadenaAuditoria.VerificarTablaAsync(db);

        Assert.True(tramo.Valida);
        Assert.False(completa.Valida);
        Assert.Equal(filas[3].IdAuditoria, completa.PrimeraFilaRota);
    }

    [Fact]
    public async Task UnaAlteracionEnLaPrimeraFilaDelTramo_SeDetecta()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Dia1, Dia1.AddDays(1), Dia1.AddDays(2));
        await AlterarAsync(db, filas[1].IdAuditoria, f => f.Accion = AuditoriaAcciones.Eliminar);

        var tramo = await CadenaAuditoria.VerificarTablaAsync(db, Dia1.AddDays(1).Date, Dia1.AddDays(3).Date);

        Assert.False(tramo.Valida);
        Assert.Equal(filas[1].IdAuditoria, tramo.PrimeraFilaRota);
    }

    [Fact]
    public async Task UnTramo_IncluyeLasFilasIntermediasAunqueSuHoraQuedeFueraPorMilisegundos()
    {
        await using var db = NuevoContexto();
        // Dos guardados simultáneos: la segunda fila de la cadena tiene una hora 1 ms anterior a la primera.
        var limite = Dia1.AddDays(1);
        await CadenaAsync(db, Dia1, limite.AddMilliseconds(1), limite.AddTicks(-1), Dia1.AddDays(2));

        var tramo = await CadenaAuditoria.VerificarTablaAsync(db, limite, null);

        Assert.True(tramo.Valida);
        Assert.Equal(3, tramo.FilasRevisadas);
    }

    [Fact]
    public async Task UnTramoSinFilas_EsValidoYNoRevisaNada()
    {
        await using var db = NuevoContexto();
        await CadenaAsync(db, Dia1, Dia1.AddDays(1));

        var tramo = await CadenaAuditoria.VerificarTablaAsync(db, Dia1.AddDays(10), Dia1.AddDays(11));

        Assert.True(tramo.Valida);
        Assert.Equal(0, tramo.FilasRevisadas);
    }

    // ------------------------------------------------------------------ el endpoint

    [Fact]
    public async Task CadaVerificacionQuedaEnLaAuditoriaYLaCadenaSigueIntegra()
    {
        await using var db = NuevoContexto();
        await CadenaAsync(db, Horas(3));

        await VerificarAsync(db);
        var (segunda, _) = await VerificarAsync(db);

        var eventos = await db.Auditoria.AsNoTracking()
            .Where(a => a.Accion == AuditoriaAcciones.VerificacionIntegridad).ToListAsync();
        Assert.Equal(2, eventos.Count);
        Assert.All(eventos, a => Assert.Equal(AuditoriaTablas.Sistema, a.Tabla));
        Assert.Contains("\"Valida\":true", eventos[0].ValorNuevo);
        Assert.Contains("\"FilasRevisadas\":3", eventos[0].ValorNuevo);
        // La segunda revisa las 3 filas y el evento de la primera, encadenado al final.
        Assert.True(segunda.Valida);
        Assert.Equal(4, segunda.FilasRevisadas);
    }

    [Fact]
    public async Task UnaCadenaRota_TambienQuedaRegistrada()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(3));
        await AlterarAsync(db, filas[1].IdAuditoria, f => f.ValorNuevo = "{}");

        await VerificarAsync(db);

        var evento = await db.Auditoria.AsNoTracking().SingleAsync(a => a.Accion == AuditoriaAcciones.VerificacionIntegridad);
        Assert.Contains("\"Valida\":false", evento.ValorNuevo);
        Assert.Contains($"\"PrimeraFilaRota\":{filas[1].IdAuditoria}", evento.ValorNuevo);
    }

    [Fact]
    public async Task ConDiasLocales_RevisaElTramoDeEsosDias()
    {
        await using var db = NuevoContexto();
        DateTime Local(int dia, int hora) =>
            DateTime.SpecifyKind(new DateTime(2026, 10, dia, hora, 0, 0), DateTimeKind.Local).ToUniversalTime();
        await CadenaAsync(db, Local(1, 8), Local(2, 0), Local(2, 23), Local(4, 12));

        var (dto, _) = await VerificarAsync(db, new DateTime(2026, 10, 2), new DateTime(2026, 10, 3));

        Assert.True(dto.Valida);
        Assert.Equal(2, dto.FilasRevisadas);
    }

    [Fact]
    public async Task ConDesdePosteriorAHasta_Devuelve400()
    {
        await using var db = NuevoContexto();

        var action = await new AuditoriaController(db).VerificarCadena(new DateTime(2026, 10, 5), new DateTime(2026, 10, 1));

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.False(await db.Auditoria.AnyAsync());
    }

    // ------------------------------------------------------------------ ancla externa contra truncados

    private static async Task<(VerificacionAuditoriaDto Dto, string Mensaje)> VerificarConAnclaAsync(ECARDbContext db,
        long? anclaFila, string? anclaHash)
    {
        var action = await new AuditoriaController(db).VerificarCadena(anclaFila: anclaFila, anclaHash: anclaHash);
        var respuesta = Assert.IsType<ApiResponse<VerificacionAuditoriaDto>>(Assert.IsType<OkObjectResult>(action.Result).Value);
        return (respuesta.Data!, respuesta.Message);
    }

    /// <summary>Borra las filas desde <paramref name="desdeId"/> hasta el final, como haría alguien con acceso a la base.</summary>
    private static async Task TruncarDesdeAsync(ECARDbContext db, long desdeId)
    {
        db.Auditoria.RemoveRange(await db.Auditoria.Where(a => a.IdAuditoria >= desdeId).ToListAsync());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task UnaCadenaValida_DevuelveSuUltimaFilaYSuHashComoAncla()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(4));

        var (dto, _) = await VerificarAsync(db);

        Assert.Equal(filas[^1].IdAuditoria, dto.UltimaFila);
        Assert.Equal(filas[^1].Hash, dto.UltimoHash);
    }

    [Fact]
    public async Task UnaCadenaRota_NoDevuelveAncla()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(4));
        await AlterarAsync(db, filas[1].IdAuditoria, f => f.ValorNuevo = "{}");

        var (dto, _) = await VerificarAsync(db);

        Assert.Null(dto.UltimaFila);
        Assert.Null(dto.UltimoHash);
    }

    [Fact]
    public async Task UnTruncadoDelFinal_NoLoVeLaCadenaPeroSiElAncla()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(5));
        var (anterior, _) = await VerificarAsync(db);

        // Se borran las dos últimas filas y todo lo posterior (también el evento de la verificación).
        await TruncarDesdeAsync(db, filas[3].IdAuditoria);
        var (sinAncla, _) = await VerificarAsync(db);
        var (conAncla, mensaje) = await VerificarConAnclaAsync(db, anterior.UltimaFila, anterior.UltimoHash);

        Assert.True(sinAncla.Valida);
        Assert.False(conAncla.Valida);
        Assert.Equal(anterior.UltimaFila, conAncla.PrimeraFilaRota);
        Assert.Contains("truncada", mensaje);
        Assert.Null(conAncla.UltimaFila);
    }

    [Fact]
    public async Task UnaCadenaReescritaEnteraDesdeElPrincipio_LaDelataElAncla()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(4));
        var (anterior, _) = await VerificarAsync(db);

        // Se altera la segunda fila y se recalcula la cadena entera, para que vuelva a cuadrar.
        var todas = await db.Auditoria.OrderBy(a => a.IdAuditoria).ToListAsync();
        todas[1].ValorNuevo = "{\"Nombre\":\"Reescrito\"}";
        string? previo = null;
        foreach (var fila in todas)
        {
            fila.HashAnterior = previo;
            fila.Hash = CadenaAuditoria.CalcularHash(fila);
            previo = fila.Hash;
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (sinAncla, _) = await VerificarAsync(db);
        var (conAncla, _) = await VerificarConAnclaAsync(db, anterior.UltimaFila, anterior.UltimoHash);

        Assert.True(sinAncla.Valida);
        Assert.False(conAncla.Valida);
        Assert.Equal(filas[^1].IdAuditoria, conAncla.PrimeraFilaRota);
    }

    [Fact]
    public async Task ConUnAnclaIntacta_EsValidaYDevuelveElAnclaNueva()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(3));
        var (anterior, _) = await VerificarAsync(db);

        var (dto, mensaje) = await VerificarConAnclaAsync(db, anterior.UltimaFila, anterior.UltimoHash!.ToLowerInvariant());

        Assert.True(dto.Valida);
        Assert.Contains("conserva la fila ancla", mensaje);
        // El ancla nueva es la última fila de ahora: el evento de la verificación anterior.
        Assert.True(dto.UltimaFila > filas[^1].IdAuditoria);
    }

    [Fact]
    public async Task ConLaCadenaRotaYUnAncla_InformaLaFilaRotaDeLaCadena()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(4));
        var (anterior, _) = await VerificarAsync(db);
        await AlterarAsync(db, filas[1].IdAuditoria, f => f.Motivo = "Añadido después");

        var (dto, _) = await VerificarConAnclaAsync(db, anterior.UltimaFila, anterior.UltimoHash);

        Assert.False(dto.Valida);
        Assert.Equal(filas[1].IdAuditoria, dto.PrimeraFilaRota);
    }

    [Theory]
    [InlineData(5L, null)]
    [InlineData(null, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(5L, "ABC123")]
    [InlineData(5L, "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public async Task ConUnAnclaIncompletaOMalFormada_Devuelve400(long? anclaFila, string? anclaHash)
    {
        await using var db = NuevoContexto();
        await CadenaAsync(db, Horas(2));

        var action = await new AuditoriaController(db).VerificarCadena(anclaFila: anclaFila, anclaHash: anclaHash);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task ElEventoDeLaVerificacionGuardaElAnclaYSiSeComprobo()
    {
        await using var db = NuevoContexto();
        var filas = await CadenaAsync(db, Horas(2));
        var (anterior, _) = await VerificarAsync(db);

        await VerificarConAnclaAsync(db, anterior.UltimaFila, anterior.UltimoHash);

        var evento = await db.Auditoria.AsNoTracking()
            .Where(a => a.Accion == AuditoriaAcciones.VerificacionIntegridad)
            .OrderByDescending(a => a.IdAuditoria)
            .FirstAsync();
        Assert.Contains($"\"AnclaFila\":{filas[^1].IdAuditoria}", evento.ValorNuevo);
        Assert.Contains("\"AnclaIntacta\":true", evento.ValorNuevo);
        Assert.Contains("\"UltimoHash\":\"", evento.ValorNuevo);
    }
}
