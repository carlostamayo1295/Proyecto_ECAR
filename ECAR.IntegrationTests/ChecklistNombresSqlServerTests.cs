using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ECAR.IntegrationTests;

/// <summary>
/// B-08 (ECAR-223): la validación de nombres repetidos de checklists (B-05) contra SQL Server. La
/// comparación Trim().ToLower() debe traducirse a SQL y dar el mismo resultado que en memoria:
/// no hay dos checklists activos con el mismo nombre, sin distinguir mayúsculas, minúsculas ni
/// espacios externos, y «Nueva versión» sigue funcionando. Cada prueba usa un nombre propio
/// porque la base es compartida.
/// </summary>
public class ChecklistNombresSqlServerTests(BaseCompartida compartida) : IClassFixture<BaseCompartida>
{
    private const string Motivo = "Motivo de prueba suficientemente largo";

    private BaseDePruebas Base => compartida.Base;

    private static string NombreUnico() => "Checklist " + Guid.NewGuid().ToString("N")[..10];

    private async Task<Checklist> ChecklistAsync(string nombre, bool activo = true, string version = "1.0")
    {
        await using var db = Base.CrearContexto();
        var checklist = new Checklist { Nombre = nombre, Version = version, Activo = activo };
        db.Checklists.Add(checklist);
        await db.SaveChangesAsync();
        return checklist;
    }

    private async Task<List<Checklist>> ConNombreAsync(string nombre)
    {
        await using var db = Base.CrearContexto();
        return await db.Checklists.AsNoTracking().Where(c => c.Nombre.Trim().ToLower() == nombre.ToLower()).ToListAsync();
    }

    private static string MensajeDeConflicto(IActionResult? resultado) =>
        Assert.IsType<ApiResponse<ChecklistDto>>(Assert.IsType<ConflictObjectResult>(resultado).Value).Message!;

    [Fact]
    public async Task Crear_con_el_nombre_de_un_activo_en_otras_mayusculas_y_espacios_devuelve_409()
    {
        var nombre = NombreUnico();
        await ChecklistAsync(nombre);

        await using var db = Base.CrearContexto();
        var resultado = await new ChecklistsController(db, new MotivoCambio())
            .CreateChecklist(new CreateChecklistDto { Nombre = "  " + nombre.ToUpperInvariant() + "  ", Version = "2.0" });

        Assert.Contains("Nueva versión", MensajeDeConflicto(resultado.Result));
        Assert.Single(await ConNombreAsync(nombre));
    }

    [Fact]
    public async Task Crear_con_el_nombre_de_uno_inactivo_esta_permitido_y_se_guarda_sin_espacios()
    {
        var nombre = NombreUnico();
        await ChecklistAsync(nombre, activo: false);

        await using var db = Base.CrearContexto();
        var resultado = await new ChecklistsController(db, new MotivoCambio())
            .CreateChecklist(new CreateChecklistDto { Nombre = "  " + nombre + "  ", Version = "2.0" });

        Assert.IsType<CreatedAtActionResult>(resultado.Result);
        var nueva = Assert.Single(await ConNombreAsync(nombre), c => c.Activo);
        Assert.Equal(nombre, nueva.Nombre);
        Assert.Equal("2.0", nueva.Version);
    }

    [Fact]
    public async Task Renombrar_con_el_nombre_de_otro_activo_devuelve_409_y_no_guarda_nada()
    {
        var ocupado = NombreUnico();
        await ChecklistAsync(ocupado);
        var propio = NombreUnico();
        var checklist = await ChecklistAsync(propio);

        await using (var db = Base.CrearContexto())
        {
            var resultado = await new ChecklistsController(db, new MotivoCambio()).UpdateChecklist(checklist.IdChecklist,
                new UpdateChecklistDto { Nombre = " " + ocupado.ToLowerInvariant() + " ", Motivo = Motivo });

            Assert.Contains("Nueva versión", MensajeDeConflicto(resultado.Result));
        }

        await using var lectura = Base.CrearContexto();
        Assert.Equal(propio, (await lectura.Checklists.AsNoTracking().SingleAsync(c => c.IdChecklist == checklist.IdChecklist)).Nombre);
    }

    [Fact]
    public async Task Reenviar_el_propio_nombre_en_otras_mayusculas_devuelve_200()
    {
        var nombre = NombreUnico();
        var checklist = await ChecklistAsync(nombre);

        await using (var db = Base.CrearContexto())
        {
            var resultado = await new ChecklistsController(db, new MotivoCambio()).UpdateChecklist(checklist.IdChecklist,
                new UpdateChecklistDto { Nombre = nombre.ToUpperInvariant(), Motivo = Motivo });

            Assert.IsType<OkObjectResult>(resultado.Result);
        }

        await using var lectura = Base.CrearContexto();
        Assert.Equal(nombre.ToUpperInvariant(),
            (await lectura.Checklists.AsNoTracking().SingleAsync(c => c.IdChecklist == checklist.IdChecklist)).Nombre);
    }

    [Fact]
    public async Task Nueva_version_sigue_funcionando_con_el_mismo_nombre()
    {
        var nombre = NombreUnico();
        var origen = await ChecklistAsync(nombre);

        await using (var db = Base.CrearContexto())
        {
            var resultado = await new ChecklistsController(db, new MotivoCambio())
                .CreateVersion(origen.IdChecklist, new CreateChecklistVersionDto { Version = "2.0" });

            Assert.IsType<CreatedAtActionResult>(resultado.Result);
        }

        var versiones = await ConNombreAsync(nombre);
        Assert.Equal(2, versiones.Count);
        Assert.All(versiones, v => Assert.Equal(nombre, v.Nombre));
        Assert.False(versiones.Single(v => v.Version == "1.0").Activo);
        Assert.True(versiones.Single(v => v.Version == "2.0").Activo);
    }
}
