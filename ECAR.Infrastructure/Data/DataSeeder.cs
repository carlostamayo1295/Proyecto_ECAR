using BCrypt.Net;
using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECAR.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(ECARDbContext context, IConfiguration configuration)
    {
        // Catálogo mínimo de equipos y categorías para poder registrar inspecciones.
        // Tiene su propia verificación para poder sembrarse en bases de datos ya creadas.
        await SeedCatalogoEquiposAsync(context);

        // Verificar si ya existen datos
        if (await context.Roles.AnyAsync())
        {
            return; // Ya hay datos, no hacer seed
        }

        // Crear roles según el SRS
        var roles = new List<Rol>
        {
            new Rol { Nombre = "Administrador" },
            new Rol { Nombre = "Técnico" },
            new Rol { Nombre = "Auditor" }
        };

        await context.Roles.AddRangeAsync(roles);
        await context.SaveChangesAsync();

        // Crear usuario administrador
        var adminPassword = configuration["AdminPassword"] ?? throw new InvalidOperationException("AdminPassword not configured in UserSecrets");
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
        var adminUsuario = new Usuario
        {
            Nombre = "Administrador ECAR",
            Correo = "admin@ecar.com",
            UsuarioAD = "admin",
            PasswordHash = adminPasswordHash,
            Activo = true
        };

        await context.Usuarios.AddAsync(adminUsuario);
        await context.SaveChangesAsync();

        // Asignar rol de Administrador al usuario admin
        var adminRol = await context.Roles.FirstOrDefaultAsync(r => r.Nombre == "Administrador");
        if (adminRol != null)
        {
            var usuarioRol = new UsuarioRol
            {
                IdUsuario = adminUsuario.IdUsuario,
                IdRol = adminRol.IdRol
            };

            await context.UsuarioRoles.AddAsync(usuarioRol);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedCatalogoEquiposAsync(ECARDbContext context)
    {
        if (await context.Equipos.AnyAsync())
        {
            return;
        }

        if (!await context.CategoriasEquipo.AnyAsync())
        {
            await context.CategoriasEquipo.AddRangeAsync(
                new CategoriaEquipo { Nombre = "Esterilización", Descripcion = "Equipos de esterilización y autoclaves" },
                new CategoriaEquipo { Nombre = "Medición", Descripcion = "Instrumentos de medición y balanzas" },
                new CategoriaEquipo { Nombre = "Centrifugación", Descripcion = "Centrífugas de laboratorio" });
            await context.SaveChangesAsync();
        }

        var categorias = await context.CategoriasEquipo.ToListAsync();
        long? CatId(string nombre) => categorias.FirstOrDefault(c => c.Nombre == nombre)?.IdCategoria;

        var equipos = new List<Equipo>
        {
            new Equipo { CodigoInterno = "AC-14", ActivoFijo = "AF-1001", NombreEquipo = "Autoclave vertical AC-14", Marca = "Tuttnauer", Modelo = "5075ELV", Fabricante = "Tuttnauer", Criticidad = "Alta", IdCategoria = CatId("Esterilización") },
            new Equipo { CodigoInterno = "BA-02", ActivoFijo = "AF-1002", NombreEquipo = "Balanza analítica BA-02", Marca = "Mettler Toledo", Modelo = "XPR205", Fabricante = "Mettler Toledo", Criticidad = "Media", IdCategoria = CatId("Medición") },
            new Equipo { CodigoInterno = "CF-09", ActivoFijo = "AF-1003", NombreEquipo = "Centrífuga CF-09", Marca = "Eppendorf", Modelo = "5810R", Fabricante = "Eppendorf", Criticidad = "Media", IdCategoria = CatId("Centrifugación") },
            new Equipo { CodigoInterno = "PH-05", ActivoFijo = "AF-1004", NombreEquipo = "pHmetro de mesa PH-05", Marca = "Hanna", Modelo = "HI5221", Fabricante = "Hanna Instruments", Criticidad = "Baja", IdCategoria = CatId("Medición") }
        };

        await context.Equipos.AddRangeAsync(equipos);
        await context.SaveChangesAsync();
    }
}