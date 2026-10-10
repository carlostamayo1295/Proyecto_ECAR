using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// Auditoría transaccional contra SQL Server (PLAN_FASE4_TAREAS §3.3 y §4, §11.10(e)): la
/// transacción revierte el cambio si la auditoría falla, el trigger impide modificarla o
/// borrarla, el bloqueo mantiene la cadena con guardados simultáneos y el modelo no tiene
/// cambios sin migración.
/// </summary>
public class AuditoriaSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private BaseDePruebas Base => compartida.Base;

    private static Equipo Equipo(string codigo) => new()
    {
        CodigoInterno = codigo,
        ActivoFijo = "AF-" + codigo,
        NombreEquipo = "Equipo " + codigo
    };

    private static string Codigo() => "IT-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task El_modelo_no_tiene_cambios_sin_migracion()
    {
        await using var db = Base.CrearContexto();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Cada_cambio_queda_en_la_auditoria_con_su_cadena()
    {
        var codigo = Codigo();
        await using (var db = Base.CrearContexto(new ContextoDePrueba { Motivo = "Alta de prueba" }))
        {
            db.Equipos.Add(Equipo(codigo));
            await db.SaveChangesAsync();
        }

        await using var lectura = Base.CrearContexto();
        var equipo = await lectura.Equipos.SingleAsync(e => e.CodigoInterno == codigo);
        var fila = await lectura.Auditoria.SingleAsync(a => a.Tabla == "Equipos" && a.RegistroId == equipo.IdEquipo);
        Assert.Equal(AuditoriaAcciones.Crear, fila.Accion);
        Assert.Equal("Alta de prueba", fila.Motivo);
        Assert.Equal("127.0.0.1", fila.DireccionIp);
        Assert.Equal(CadenaAuditoria.CalcularHash(fila), fila.Hash);
        var anterior = await lectura.Auditoria.Where(a => a.IdAuditoria < fila.IdAuditoria)
            .OrderByDescending(a => a.IdAuditoria).Select(a => a.Hash).FirstOrDefaultAsync();
        Assert.Equal(anterior, fila.HashAnterior);
    }

    [Fact]
    public async Task Si_la_auditoria_falla_el_cambio_se_revierte()
    {
        var codigo = Codigo();
        var filasAntes = await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria");

        // Un motivo de 600 caracteres no cabe en Auditoria.Motivo (500): el INSERT de la
        // auditoría falla después de haber guardado el equipo, dentro de la misma transacción.
        await using (var db = Base.CrearContexto(new ContextoDePrueba { Motivo = new string('x', 600) }))
        {
            db.Equipos.Add(Equipo(codigo));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var lectura = Base.CrearContexto();
        Assert.False(await lectura.Equipos.AnyAsync(e => e.CodigoInterno == codigo));
        Assert.Equal(filasAntes, await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria"));
    }

    [Fact]
    public async Task La_auditoria_entra_en_la_transaccion_de_quien_llama()
    {
        var codigo = Codigo();
        var filasAntes = await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria");

        await using (var db = Base.CrearContexto(new ContextoDePrueba()))
        {
            await using var transaccion = await db.Database.BeginTransactionAsync();
            db.Equipos.Add(Equipo(codigo));
            await db.SaveChangesAsync();
            await transaccion.RollbackAsync();
        }

        await using var lectura = Base.CrearContexto();
        Assert.False(await lectura.Equipos.AnyAsync(e => e.CodigoInterno == codigo));
        Assert.Equal(filasAntes, await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria"));
    }

    [Fact]
    public async Task El_trigger_impide_modificar_o_borrar_la_auditoria_incluso_por_SQL()
    {
        await using (var db = Base.CrearContexto(new ContextoDePrueba()))
        {
            db.Equipos.Add(Equipo(Codigo()));
            await db.SaveChangesAsync();
        }

        var filas = await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria");
        var update = await Assert.ThrowsAsync<SqlException>(() =>
            Base.EjecutarAsync("UPDATE Auditoria SET Accion = N'Crear' WHERE IdAuditoria = (SELECT MAX(IdAuditoria) FROM Auditoria)"));
        var delete = await Assert.ThrowsAsync<SqlException>(() => Base.EjecutarAsync("DELETE FROM Auditoria"));

        Assert.Equal(51000, update.Number);
        Assert.Equal(51000, delete.Number);
        Assert.Equal(filas, await Base.EscalarAsync<int>("SELECT COUNT(*) FROM Auditoria"));
    }

    [Fact]
    public async Task Guardados_simultaneos_no_rompen_la_cadena()
    {
        // Cada guardado lee el último hash con UPDLOCK, HOLDLOCK: los demás esperan su turno.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            for (var i = 0; i < 5; i++)
            {
                await using var db = Base.CrearContexto(new ContextoDePrueba());
                db.Equipos.Add(Equipo(Codigo()));
                await db.SaveChangesAsync();
            }
        }));

        await using var lectura = Base.CrearContexto();
        var cadena = await lectura.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).ToListAsync();
        var resultado = CadenaAuditoria.Verificar(cadena, null);
        Assert.True(resultado.Valida, $"La cadena se rompe en la fila {resultado.PrimeraFilaRota}");
        Assert.True(resultado.FilasRevisadas >= 20);
    }

    [Fact]
    public async Task Desactivar_un_usuario_renueva_su_sello_en_la_base()
    {
        long idUsuario;
        string sello;
        await using (var db = Base.CrearContexto(new ContextoDePrueba()))
        {
            var usuario = new Usuario
            {
                Nombre = "Usuario de integración",
                Correo = Codigo() + "@pruebas.ecar",
                PasswordHash = "hash-de-prueba"
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            idUsuario = usuario.IdUsuario;
            sello = usuario.SecurityStamp;

            usuario.Activo = false;
            await db.SaveChangesAsync();
        }

        await using var lectura = Base.CrearContexto();
        var guardado = await lectura.Usuarios.SingleAsync(u => u.IdUsuario == idUsuario);
        Assert.NotEqual(sello, guardado.SecurityStamp);
        Assert.Equal(AuditoriaAcciones.Desactivar, await lectura.Auditoria
            .Where(a => a.Tabla == "Usuarios" && a.RegistroId == idUsuario)
            .OrderByDescending(a => a.IdAuditoria).Select(a => a.Accion).FirstAsync());
    }
}
