using ECAR.API.Controllers;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECAR.API.Tests;

/// <summary>
/// Reactivación de usuarios, equipos y checklists. DELETE es el borrado lógico y siempre
/// desactiva; antes el botón "Activar" del cliente llamaba también a DELETE en Usuarios y
/// Checklists, y no hacía nada.
/// </summary>
public class ActivacionTests
{
    [Fact]
    public async Task UsuarioDesactivadoSePuedeReactivar()
    {
        await using var context = CreateContext();
        var usuario = new Usuario { Nombre = "Técnico", Correo = "tecnico@ecar.com", Activo = true };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        var controller = new UsuariosController(context);

        await controller.DeleteUsuario(usuario.IdUsuario);
        Assert.False((await context.Usuarios.FindAsync(usuario.IdUsuario))!.Activo);

        var result = await controller.ActivarUsuario(usuario.IdUsuario);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True((await context.Usuarios.FindAsync(usuario.IdUsuario))!.Activo);
    }

    [Fact]
    public async Task EquipoDesactivadoSePuedeReactivar()
    {
        await using var context = CreateContext();
        var equipo = new Equipo { CodigoInterno = "EQ-1", ActivoFijo = "AF-1", NombreEquipo = "Balanza", Activo = false };
        context.Equipos.Add(equipo);
        await context.SaveChangesAsync();

        var result = await CreateEquiposController(context).ActivarEquipo(equipo.IdEquipo);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True((await context.Equipos.FindAsync(equipo.IdEquipo))!.Activo);
    }

    [Fact]
    public async Task ActivarUnaVersionDeChecklistDesactivaLasOtrasDelMismoNombre()
    {
        await using var context = CreateContext();
        var v1 = new Checklist { Nombre = "Diario balanza", Version = "1.0", Activo = false };
        var v2 = new Checklist { Nombre = "Diario balanza", Version = "2.0", Activo = true };
        var otro = new Checklist { Nombre = "Mensual horno", Version = "1.0", Activo = true };
        context.Checklists.AddRange(v1, v2, otro);
        await context.SaveChangesAsync();

        var result = await new ChecklistsController(context).ActivarChecklist(v1.IdChecklist);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.Contains("2.0", response.Message);
        Assert.True((await context.Checklists.FindAsync(v1.IdChecklist))!.Activo);
        Assert.False((await context.Checklists.FindAsync(v2.IdChecklist))!.Activo);
        // Un checklist con otro nombre no se toca.
        Assert.True((await context.Checklists.FindAsync(otro.IdChecklist))!.Activo);
    }

    [Fact]
    public async Task ActivarUnRegistroInexistenteDevuelve404()
    {
        await using var context = CreateContext();

        Assert.IsType<NotFoundObjectResult>((await new UsuariosController(context).ActivarUsuario(999)).Result);
        Assert.IsType<NotFoundObjectResult>((await CreateEquiposController(context).ActivarEquipo(999)).Result);
        Assert.IsType<NotFoundObjectResult>((await new ChecklistsController(context).ActivarChecklist(999)).Result);
    }

    private static EquiposController CreateEquiposController(ECARDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cliente:BaseUrl"] = "https://app.ecar.test/" })
            .Build();
        return new EquiposController(context, configuration);
    }

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ECARDbContext(options);
    }
}
