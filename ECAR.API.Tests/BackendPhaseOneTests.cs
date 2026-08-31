using ECAR.API.Controllers;
using ECAR.API.Services;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ECAR.API.Tests;

public class BackendPhaseOneTests
{
    [Fact]
    public async Task ActiveDirectoryAdapterDoesNotConnectWhenDisabled()
    {
        var service = new LdapActiveDirectoryAuthService(
            Options.Create(new ActiveDirectoryOptions { Enabled = false }),
            NullLogger<LdapActiveDirectoryAuthService>.Instance);

        Assert.False(await service.AuthenticateAsync("usuario", "clave"));
    }

    [Fact]
    public async Task ActiveDirectoryUserCanBeCreatedWithoutLocalPassword()
    {
        await using var context = CreateContext();
        var controller = new UsuariosController(context);

        var result = await controller.CreateUsuario(new CreateUsuarioDto
        {
            Nombre = "Usuario Directorio",
            Correo = "directorio@ecar.com",
            UsuarioAD = "usuario.directorio"
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(string.Empty, (await context.Usuarios.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task UpdatingUserWithUnknownRoleDoesNotSavePartialChanges()
    {
        await using var context = CreateContext();
        var user = new Usuario
        {
            Nombre = "Nombre original",
            Correo = "usuario@ecar.com",
            PasswordHash = "hash",
            Activo = true
        };
        context.Usuarios.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsuariosController(context);
        var result = await controller.UpdateUsuario(user.IdUsuario, new UpdateUsuarioDto
        {
            Nombre = "Nombre que no debe guardarse",
            RoleIds = [999]
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        context.ChangeTracker.Clear();
        Assert.Equal("Nombre original", (await context.Usuarios.SingleAsync()).Nombre);
    }

    [Fact]
    public async Task LastActiveAdministratorCannotBeDisabled()
    {
        await using var context = CreateContext();
        var adminRole = new Rol { Nombre = "Administrador" };
        var admin = new Usuario
        {
            Nombre = "Administrador",
            Correo = "admin@ecar.com",
            PasswordHash = "hash",
            Activo = true
        };
        admin.UsuarioRoles.Add(new UsuarioRol { Rol = adminRole });
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();

        var controller = new UsuariosController(context);
        var result = await controller.UpdateUsuario(admin.IdUsuario, new UpdateUsuarioDto { Activo = false });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<UsuarioDto>>(badRequest.Value);
        Assert.Contains("último administrador", response.Message);
        Assert.True((await context.Usuarios.SingleAsync()).Activo);
    }

    [Fact]
    public async Task CategoryAssignedToEquipmentCannotBeDeleted()
    {
        await using var context = CreateContext();
        var category = new CategoriaEquipo { Nombre = "Laboratorio" };
        context.Equipos.Add(new Equipo
        {
            CodigoInterno = "EQ-001",
            ActivoFijo = "AF-001",
            NombreEquipo = "Balanza",
            Categoria = category
        });
        await context.SaveChangesAsync();

        var controller = new CategoriasEquipoController(context);
        var result = await controller.DeleteCategoria(category.IdCategoria);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.True(await context.CategoriasEquipo.AnyAsync());
    }

    [Fact]
    public async Task UpdatingRoleAssignmentReturnsTheNewUserAndRole()
    {
        await using var context = CreateContext();
        var auditor = new Rol { Nombre = "Auditor" };
        var tecnico = new Rol { Nombre = "Técnico" };
        var firstUser = new Usuario
        {
            Nombre = "Primer usuario",
            Correo = "primero@ecar.com",
            PasswordHash = "hash",
            Activo = true
        };
        var secondUser = new Usuario
        {
            Nombre = "Segundo usuario",
            Correo = "segundo@ecar.com",
            PasswordHash = "hash",
            Activo = true
        };
        var assignment = new UsuarioRol { Usuario = firstUser, Rol = auditor };
        context.AddRange(assignment, secondUser, tecnico);
        await context.SaveChangesAsync();

        var controller = new UsuariosRolController(context);
        var result = await controller.UpdateUsuarioRol(assignment.Id, new UpdateUsuarioRolDto
        {
            IdUsuario = secondUser.IdUsuario,
            IdRol = tecnico.IdRol
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<UsuarioRolDto>>(ok.Value);
        Assert.Equal("Segundo usuario", response.Data!.UsuarioNombre);
        Assert.Equal("Técnico", response.Data.RolNombre);
    }

    [Fact]
    public async Task SeederCompletesPartiallyPopulatedDatabase()
    {
        await using var context = CreateContext();
        context.Roles.Add(new Rol { Nombre = "Auditor" });
        context.CategoriasEquipo.Add(new CategoriaEquipo { Nombre = "Instrumentación" });
        context.Ubicaciones.Add(new Ubicacion { Planta = "Planta Principal", Area = "Producción" });
        await context.SaveChangesAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminPassword"] = "ClaveTemporalSegura123!"
            })
            .Build();

        await DataSeeder.SeedDataAsync(context, configuration);
        await DataSeeder.SeedDataAsync(context, configuration);

        Assert.Equal(3, await context.Roles.CountAsync());
        Assert.Equal(1, await context.Usuarios.CountAsync(u => u.Correo == "admin@ecar.com"));
        Assert.Equal(1, await context.UsuarioRoles.CountAsync());
        Assert.Equal(7, await context.CategoriasEquipo.CountAsync());
        Assert.Equal(4, await context.Equipos.CountAsync());
        Assert.Equal(3, await context.Ubicaciones.CountAsync());
    }

    private static ECARDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ECARDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ECARDbContext(options);
    }
}
