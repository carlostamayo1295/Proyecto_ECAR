using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Hallazgos contra SQL Server: lo que el proveedor en memoria no comprueba. La firma y sus
/// hallazgos se guardan en una sola transacción (si un hallazgo falla, la inspección sigue en
/// curso), el índice único impide duplicar una novedad y la FK impide borrar una inspección
/// que tiene hallazgos.
/// </summary>
public class HallazgosSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private BaseDePruebas Base => compartida.Base;

    private sealed record Datos(long IdInspeccion, long IdPregunta, long IdRespuesta, long IdUsuario);

    /// <summary>Una inspección en curso con una respuesta "No" a una pregunta Sí/No.</summary>
    private async Task<Datos> InspeccionConNovedadAsync()
    {
        var codigo = "IT-" + Guid.NewGuid().ToString("N")[..10];
        await using var db = Base.CrearContexto();

        var usuario = new Usuario { Nombre = "Inspector", Correo = codigo + "@ecar.test", PasswordHash = "hash", Activo = true };
        var equipo = new Equipo { CodigoInterno = codigo, ActivoFijo = "AF-" + codigo, NombreEquipo = "Equipo " + codigo };
        var pregunta = new PreguntaChecklist { Pregunta = "¿Está limpio?", TipoRespuesta = TiposRespuesta.SiNo, Obligatoria = true };
        var checklist = new Checklist { Nombre = "Checklist " + codigo, Version = "1.0", Activo = true, Preguntas = [pregunta] };
        db.AddRange(usuario, equipo, checklist);
        await db.SaveChangesAsync();

        var inspeccion = new Inspeccion
        {
            IdEquipo = equipo.IdEquipo,
            IdUsuario = usuario.IdUsuario,
            IdChecklist = checklist.IdChecklist,
            FechaInspeccion = DateTime.UtcNow,
            Estado = InspeccionEstados.EnCurso
        };
        db.Inspecciones.Add(inspeccion);
        await db.SaveChangesAsync();

        var respuesta = new RespuestaInspeccion
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "No",
            Observacion = "Polvo acumulado"
        };
        db.RespuestasInspeccion.Add(respuesta);
        await db.SaveChangesAsync();

        return new Datos(inspeccion.IdInspeccion, pregunta.IdPregunta, respuesta.IdRespuesta, usuario.IdUsuario);
    }

    private static Hallazgo Novedad(Datos datos) => new()
    {
        IdInspeccion = datos.IdInspeccion,
        IdPregunta = datos.IdPregunta,
        IdRespuesta = datos.IdRespuesta,
        Descripcion = "¿Está limpio?: Polvo acumulado",
        Origen = HallazgoOrigenes.Novedad,
        IdUsuarioRegistro = datos.IdUsuario
    };

    [Fact]
    public async Task Si_un_hallazgo_falla_la_firma_entera_se_revierte()
    {
        var datos = await InspeccionConNovedadAsync();

        // Lo mismo que hace la firma (cerrar y crear los hallazgos en un SaveChanges), pero con
        // la novedad repetida: el índice único rechaza el segundo hallazgo.
        await using (var db = Base.CrearContexto())
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            inspeccion.Estado = InspeccionEstados.Cerrada;
            inspeccion.FechaCierre = DateTime.UtcNow;
            db.Hallazgos.AddRange(Novedad(datos), Novedad(datos));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        var guardada = await lectura.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.FechaCierre);
        Assert.False(await lectura.Hallazgos.AnyAsync(h => h.IdInspeccion == datos.IdInspeccion));
        Assert.False(await lectura.Auditoria.AnyAsync(a => a.Tabla == "Inspecciones"
            && a.RegistroId == datos.IdInspeccion && a.Accion == AuditoriaAcciones.Firmar));
    }

    [Fact]
    public async Task La_base_no_admite_dos_hallazgos_de_la_misma_novedad()
    {
        var datos = await InspeccionConNovedadAsync();
        await using (var db = Base.CrearContexto())
        {
            db.Hallazgos.Add(Novedad(datos));
            await db.SaveChangesAsync();
        }

        await using var otra = Base.CrearContexto();
        otra.Hallazgos.Add(Novedad(datos));
        await Assert.ThrowsAsync<DbUpdateException>(() => otra.SaveChangesAsync());
    }

    [Fact]
    public async Task Los_hallazgos_manuales_no_tienen_limite_por_inspeccion()
    {
        var datos = await InspeccionConNovedadAsync();
        await using var db = Base.CrearContexto();

        db.Hallazgos.AddRange(
            new Hallazgo { IdInspeccion = datos.IdInspeccion, Descripcion = "Uno", IdUsuarioRegistro = datos.IdUsuario },
            new Hallazgo { IdInspeccion = datos.IdInspeccion, Descripcion = "Dos", IdUsuarioRegistro = datos.IdUsuario });
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Hallazgos.CountAsync(h => h.IdInspeccion == datos.IdInspeccion));
    }

    [Fact]
    public async Task Una_inspeccion_con_hallazgos_no_se_puede_borrar()
    {
        var datos = await InspeccionConNovedadAsync();
        await using (var db = Base.CrearContexto())
        {
            db.Hallazgos.Add(Novedad(datos));
            await db.SaveChangesAsync();
        }

        await using (var db = Base.CrearContexto())
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            db.Inspecciones.Remove(inspeccion);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        Assert.True(await lectura.Hallazgos.AnyAsync(h => h.IdInspeccion == datos.IdInspeccion));
    }
}
