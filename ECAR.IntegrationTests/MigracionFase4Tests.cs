using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// La migración Fase4HallazgosAuditoria sobre una base con datos de la Fase 3: convierte los
/// hallazgos, da un sello a cada usuario y sella la auditoría existente con la misma fórmula que
/// usa la aplicación, de modo que la cadena se verifica entera y detecta una fila alterada.
/// </summary>
public class MigracionFase4Tests
{
    private const string UltimaMigracionFase3 = "20260921114232_Fase3BaseInspecciones";

    [Fact]
    public async Task Convierte_los_datos_de_la_Fase_3_y_sella_la_auditoria_existente()
    {
        await using var baseDePruebas = await BaseDePruebas.CrearAsync(UltimaMigracionFase3);
        await baseDePruebas.EjecutarAsync("""
            SET QUOTED_IDENTIFIER ON;
            INSERT INTO Usuarios (Nombre, Correo, UsuarioAD, PasswordHash, Activo)
                VALUES (N'Ana', N'ana@pruebas.ecar', N'ana', N'x', 1), (N'Beto', N'beto@pruebas.ecar', N'beto', N'y', 1);
            INSERT INTO Equipos (CodigoInterno, ActivoFijo, NombreEquipo, Activo, FechaCreacion)
                VALUES (N'E1', N'A1', N'Balanza', 1, SYSUTCDATETIME());
            INSERT INTO Checklists (Nombre, Version, Activo, FechaCreacion) VALUES (N'Diario', N'1.0', 1, SYSUTCDATETIME());
            INSERT INTO Inspecciones (IdEquipo, IdUsuario, IdChecklist, FechaInspeccion, Estado)
                SELECT e.IdEquipo, u.IdUsuario, c.IdChecklist, SYSUTCDATETIME(), N'EnCurso'
                FROM Equipos e CROSS JOIN Checklists c CROSS JOIN Usuarios u WHERE u.Correo = N'beto@pruebas.ecar';
            INSERT INTO Hallazgos (IdInspeccion, Descripcion, Criticidad, Estado, FechaRegistro)
                SELECT IdInspeccion, N'Sin estado', N'Alta', NULL, SYSUTCDATETIME() FROM Inspecciones;
            INSERT INTO Hallazgos (IdInspeccion, Descripcion, Criticidad, Estado, FechaRegistro)
                SELECT IdInspeccion, N'Ya cerrado', N'Baja', N'Cerrado', SYSUTCDATETIME() FROM Inspecciones;
            INSERT INTO Auditoria (Tabla, RegistroId, Accion, ValorAnterior, ValorNuevo, Usuario, FechaHora) VALUES
                (N'Equipos', 1, N'Crear', NULL, N'{"Nombre":"Balanza ñ|ó"}', N'admin@ecar.com', '2026-10-01T10:00:00.1234567'),
                (N'Usuarios', 2, N'UPDATE', N'Activo=1', N'Activo=0', N'admin@ecar.com', '2026-10-02T11:00:00'),
                (N'Sistema', 0, N'Otra', NULL, NULL, N'x', '2026-10-03T00:00:00.0000001');
            """);

        await using (var db = baseDePruebas.CrearContexto())
        {
            await db.Database.MigrateAsync();
        }

        await using var lectura = baseDePruebas.CrearContexto();

        var hallazgos = await lectura.Hallazgos.AsNoTracking().OrderBy(h => h.IdHallazgo).ToListAsync();
        var inspector = await lectura.Usuarios.Where(u => u.Correo == "beto@pruebas.ecar").Select(u => u.IdUsuario).SingleAsync();
        Assert.Equal([HallazgoEstados.Abierto, HallazgoEstados.Cerrado], hallazgos.Select(h => h.Estado));
        Assert.All(hallazgos, h => Assert.Equal(HallazgoOrigenes.Manual, h.Origen));
        Assert.All(hallazgos, h => Assert.Equal(inspector, h.IdUsuarioRegistro));

        var sellos = await lectura.Usuarios.Select(u => u.SecurityStamp).ToListAsync();
        Assert.All(sellos, s => Assert.Equal(32, s.Length));
        Assert.Equal(sellos.Count, sellos.Distinct().Count());

        // El sellado en T-SQL coincide con CadenaAuditoria: la cadena se verifica entera.
        var legado = await lectura.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).ToListAsync();
        Assert.Equal(new ResultadoCadena(true, 3, null, legado[^1].Hash), CadenaAuditoria.Verificar(legado, null));
        Assert.Null(legado[0].HashAnterior);

        // Lo que se escribe después cuelga de la última fila sellada.
        await using (var db = baseDePruebas.CrearContexto(new ContextoDePrueba()))
        {
            db.Roles.Add(new Rol { Nombre = "Auditor" });
            await db.SaveChangesAsync();
        }

        var cadena = await lectura.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).ToListAsync();
        Assert.Equal(legado[^1].Hash, cadena[^1].HashAnterior);
        Assert.True(CadenaAuditoria.Verificar(cadena, null).Valida);

        // Alguien con permisos de administrador de la base desactiva el trigger y cambia una fila:
        // la verificación lo detecta en esa fila.
        await baseDePruebas.EjecutarAsync($"""
            DISABLE TRIGGER [{ECARDbContext.TriggerAuditoriaSoloInsercion}] ON [Auditoria];
            UPDATE [Auditoria] SET [ValorNuevo] = N'Activo=1' WHERE [IdAuditoria] = {cadena[1].IdAuditoria};
            ENABLE TRIGGER [{ECARDbContext.TriggerAuditoriaSoloInsercion}] ON [Auditoria];
            """);
        await using var despues = baseDePruebas.CrearContexto();
        var alterada = await despues.Auditoria.AsNoTracking().OrderBy(a => a.IdAuditoria).ToListAsync();
        var resultado = CadenaAuditoria.Verificar(alterada, null);
        Assert.False(resultado.Valida);
        Assert.Equal(cadena[1].IdAuditoria, resultado.PrimeraFilaRota);
    }
}
