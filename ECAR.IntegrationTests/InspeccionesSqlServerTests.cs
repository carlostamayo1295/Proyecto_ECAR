using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Anulación de inspecciones contra SQL Server: ninguna FK borra en cascada una inspección ni sus
/// datos. Borrar por SQL la inspección, su equipo, su inspector, su checklist o una pregunta
/// respondida lo rechaza la propia base y no se pierde nada.
/// </summary>
public class InspeccionesSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private BaseDePruebas Base => compartida.Base;

    private sealed record Datos(long IdInspeccion, long IdEquipo, long IdUsuario, long IdChecklist, long IdPregunta);

    /// <summary>Una inspección en curso con una respuesta y una evidencia.</summary>
    private async Task<Datos> InspeccionConDatosAsync()
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

        db.RespuestasInspeccion.Add(new RespuestaInspeccion
        {
            IdInspeccion = inspeccion.IdInspeccion,
            IdPregunta = pregunta.IdPregunta,
            Respuesta = "Si"
        });
        db.Evidencias.Add(new Evidencia
        {
            IdInspeccion = inspeccion.IdInspeccion,
            Archivo = "2026/10/foto.jpg",
            NombreOriginal = "foto.jpg",
            TipoContenido = "image/jpeg",
            TamanoBytes = 4,
            FechaCarga = DateTime.UtcNow,
            IdUsuarioCarga = usuario.IdUsuario
        });
        await db.SaveChangesAsync();

        return new Datos(inspeccion.IdInspeccion, equipo.IdEquipo, usuario.IdUsuario, checklist.IdChecklist, pregunta.IdPregunta);
    }

    private async Task AssertConservaTodoAsync(Datos datos)
    {
        await using var lectura = Base.CrearContexto();
        Assert.True(await lectura.Inspecciones.AnyAsync(i => i.IdInspeccion == datos.IdInspeccion));
        Assert.Equal(1, await lectura.RespuestasInspeccion.CountAsync(r => r.IdInspeccion == datos.IdInspeccion));
        Assert.Equal(1, await lectura.Evidencias.CountAsync(e => e.IdInspeccion == datos.IdInspeccion));
        Assert.True(await lectura.PreguntasChecklist.AnyAsync(p => p.IdPregunta == datos.IdPregunta));
    }

    [Theory]
    [InlineData("DELETE FROM Inspecciones WHERE IdInspeccion = {0}", nameof(Datos.IdInspeccion))]
    [InlineData("DELETE FROM Equipos WHERE IdEquipo = {0}", nameof(Datos.IdEquipo))]
    [InlineData("DELETE FROM Checklists WHERE IdChecklist = {0}", nameof(Datos.IdChecklist))]
    [InlineData("DELETE FROM PreguntasChecklist WHERE IdPregunta = {0}", nameof(Datos.IdPregunta))]
    public async Task Ni_por_SQL_se_borra_una_inspeccion_ni_sus_datos(string sql, string clave)
    {
        var datos = await InspeccionConDatosAsync();
        var id = (long)typeof(Datos).GetProperty(clave)!.GetValue(datos)!;

        await Assert.ThrowsAsync<SqlException>(() => Base.EjecutarAsync(string.Format(sql, id)));

        await AssertConservaTodoAsync(datos);
    }

    [Fact]
    public async Task Si_la_auditoria_de_la_anulacion_falla_la_inspeccion_sigue_en_curso()
    {
        var datos = await InspeccionConDatosAsync();

        // Lo mismo que hace POST {id}/anular, pero con un motivo de auditoría de 600 caracteres,
        // que no cabe en Auditoria.Motivo (500): la fila de auditoría falla después de haber
        // guardado la inspección, dentro de la misma transacción.
        await using (var db = Base.CrearContexto(new ContextoDePrueba { Motivo = new string('m', 600) }))
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            inspeccion.Estado = InspeccionEstados.Anulada;
            inspeccion.MotivoAnulacion = "Se inició sobre el equipo equivocado";
            inspeccion.FechaAnulacion = DateTime.UtcNow;
            inspeccion.IdUsuarioAnulacion = datos.IdUsuario;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        var guardada = await lectura.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
        Assert.Equal(InspeccionEstados.EnCurso, guardada.Estado);
        Assert.Null(guardada.MotivoAnulacion);
        Assert.False(await lectura.Auditoria.AnyAsync(a =>
            a.Tabla == "Inspecciones" && a.RegistroId == datos.IdInspeccion && a.Accion == AuditoriaAcciones.Anular));
    }

    [Fact]
    public async Task Una_anulacion_valida_queda_con_su_fila_de_auditoria_y_conserva_todo()
    {
        var datos = await InspeccionConDatosAsync();

        await using (var db = Base.CrearContexto(new ContextoDePrueba { Motivo = "Se inició sobre el equipo equivocado" }))
        {
            var inspeccion = await db.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
            inspeccion.Estado = InspeccionEstados.Anulada;
            inspeccion.MotivoAnulacion = "Se inició sobre el equipo equivocado";
            inspeccion.FechaAnulacion = DateTime.UtcNow;
            inspeccion.IdUsuarioAnulacion = datos.IdUsuario;
            await db.SaveChangesAsync();
        }

        await AssertConservaTodoAsync(datos);
        await using var lectura = Base.CrearContexto();
        var fila = await lectura.Auditoria.SingleAsync(a =>
            a.Tabla == "Inspecciones" && a.RegistroId == datos.IdInspeccion && a.Accion == AuditoriaAcciones.Anular);
        Assert.Equal("Se inició sobre el equipo equivocado", fila.Motivo);
    }

    [Fact]
    public async Task Firmar_y_anular_a_la_vez_la_segunda_falla_por_el_rowversion()
    {
        var datos = await InspeccionConDatosAsync();

        // Las dos peticiones leen la inspección en curso antes de que ninguna guarde.
        await using var anular = Base.CrearContexto();
        await using var firmar = Base.CrearContexto();
        var paraAnular = await anular.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
        var paraFirmar = await firmar.Inspecciones.SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);

        paraAnular.Estado = InspeccionEstados.Anulada;
        paraAnular.MotivoAnulacion = "Se inició sobre el equipo equivocado";
        await anular.SaveChangesAsync();

        paraFirmar.Estado = InspeccionEstados.Cerrada;
        paraFirmar.FirmaDigital = "firma";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => firmar.SaveChangesAsync());

        await using var lectura = Base.CrearContexto();
        var guardada = await lectura.Inspecciones.AsNoTracking().SingleAsync(i => i.IdInspeccion == datos.IdInspeccion);
        Assert.Equal(InspeccionEstados.Anulada, guardada.Estado);
        Assert.Null(guardada.FirmaDigital);
    }

    [Fact]
    public async Task Borrar_el_inspector_por_SQL_no_arrastra_sus_inspecciones()
    {
        var datos = await InspeccionConDatosAsync();

        await Assert.ThrowsAsync<SqlException>(() =>
            Base.EjecutarAsync($"DELETE FROM Usuarios WHERE IdUsuario = {datos.IdUsuario}"));

        await AssertConservaTodoAsync(datos);
    }
}
