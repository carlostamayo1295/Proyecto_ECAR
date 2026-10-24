using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// B-05 (ECAR-222): no puede haber dos checklists activos con el mismo nombre, sin distinguir
/// mayúsculas, minúsculas ni espacios externos. Crear o renombrar devuelve 409 y pide usar
/// «Nueva versión»; ese flujo no cambia.
/// </summary>
public class ChecklistNombreDuplicadoTests
{
    private const string Motivo = "Motivo de prueba suficientemente largo";

    private static async Task<(ECARDbContext Db, Checklist Activo, Checklist Otro)> SeedAsync()
    {
        var db = new ECARDbContext(new DbContextOptionsBuilder<ECARDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var activo = new Checklist { Nombre = "Diario balanza", Version = "1.0", Activo = true };
        var otro = new Checklist { Nombre = "Mensual horno", Version = "1.0", Activo = true };
        var inactivo = new Checklist { Nombre = "Semanal bomba", Version = "1.0", Activo = false };
        db.Checklists.AddRange(activo, otro, inactivo);
        await db.SaveChangesAsync();
        return (db, activo, otro);
    }

    private static ChecklistsController Controlador(ECARDbContext db) => new(db, new MotivoCambio());

    private static string Mensaje(IActionResult? resultado) =>
        Assert.IsType<ApiResponse<ChecklistDto>>(Assert.IsType<ConflictObjectResult>(resultado).Value).Message!;

    [Theory]
    [InlineData("Diario balanza")]
    [InlineData("  diario BALANZA  ")]
    public async Task Crear_con_el_nombre_de_un_activo_devuelve_409_y_pide_nueva_version(string nombre)
    {
        var (db, _, _) = await SeedAsync();

        var resultado = await Controlador(db).CreateChecklist(new CreateChecklistDto { Nombre = nombre, Version = "2.0" });

        Assert.Contains("Nueva versión", Mensaje(resultado.Result));
        Assert.Equal(3, await db.Checklists.CountAsync());
    }

    [Fact]
    public async Task Crear_con_el_nombre_de_uno_inactivo_esta_permitido()
    {
        var (db, _, _) = await SeedAsync();

        var resultado = await Controlador(db).CreateChecklist(new CreateChecklistDto { Nombre = " Semanal bomba ", Version = "2.0" });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);
        Assert.True(await db.Checklists.AnyAsync(c => c.Nombre == "Semanal bomba" && c.Version == "2.0" && c.Activo));
    }

    [Theory]
    [InlineData("Mensual horno")]
    [InlineData(" MENSUAL horno ")]
    public async Task Renombrar_con_el_nombre_de_otro_activo_devuelve_409_y_no_cambia_nada(string nombre)
    {
        var (db, activo, _) = await SeedAsync();

        var resultado = await Controlador(db).UpdateChecklist(activo.IdChecklist,
            new UpdateChecklistDto { Nombre = nombre, Motivo = Motivo });

        Assert.Contains("Nueva versión", Mensaje(resultado.Result));
        db.ChangeTracker.Clear();
        Assert.Equal("Diario balanza", (await db.Checklists.FindAsync(activo.IdChecklist))!.Nombre);
    }

    [Theory]
    [InlineData("Diario balanza calibrada", "Diario balanza calibrada")]
    [InlineData("Diario balanza", "Diario balanza")]
    [InlineData(" DIARIO balanza ", "DIARIO balanza")]
    public async Task Renombrar_a_un_nombre_libre_o_al_propio_devuelve_200(string nombre, string guardado)
    {
        var (db, activo, _) = await SeedAsync();

        var resultado = await Controlador(db).UpdateChecklist(activo.IdChecklist,
            new UpdateChecklistDto { Nombre = nombre, Motivo = Motivo });

        Assert.IsType<OkObjectResult>(resultado.Result);
        db.ChangeTracker.Clear();
        Assert.Equal(guardado, (await db.Checklists.FindAsync(activo.IdChecklist))!.Nombre);
    }

    [Fact]
    public async Task Nueva_version_sigue_funcionando_con_el_mismo_nombre()
    {
        var (db, activo, _) = await SeedAsync();

        var resultado = await Controlador(db).CreateVersion(activo.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);
        db.ChangeTracker.Clear();
        var versiones = await db.Checklists.Where(c => c.Nombre == "Diario balanza").ToListAsync();
        Assert.Equal(2, versiones.Count);
        Assert.False(versiones.Single(v => v.Version == "1.0").Activo);
        Assert.True(versiones.Single(v => v.Version == "2.0").Activo);
    }
}
