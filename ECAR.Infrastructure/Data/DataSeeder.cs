using BCrypt.Net;
using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECAR.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(ECARDbContext context, IConfiguration configuration)
    {
        // Seed each group separately so a partially populated database can be completed safely.
        var requiredRoleNames = new[] { "Administrador", "Técnico", "Auditor" };
        var existingRoleNames = await context.Roles.Select(r => r.Nombre).ToListAsync();
        var missingRoles = requiredRoleNames
            .Except(existingRoleNames, StringComparer.OrdinalIgnoreCase)
            .Select(nombre => new Rol { Nombre = nombre })
            .ToList();
        if (missingRoles.Count > 0)
        {
            await context.Roles.AddRangeAsync(missingRoles);
            await context.SaveChangesAsync();
        }

        var adminUsuario = await context.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == "admin@ecar.com");
        if (adminUsuario == null)
        {
            var adminPassword = configuration["AdminPassword"] ??
                throw new InvalidOperationException("AdminPassword no está configurado en User Secrets");
            adminUsuario = new Usuario
            {
                Nombre = "Administrador ECAR",
                Correo = "admin@ecar.com",
                UsuarioAD = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Activo = true
            };
            await context.Usuarios.AddAsync(adminUsuario);
            await context.SaveChangesAsync();
        }

        var adminRol = await context.Roles.SingleAsync(r => r.Nombre == "Administrador");
        if (!await context.UsuarioRoles.AnyAsync(ur =>
                ur.IdUsuario == adminUsuario.IdUsuario && ur.IdRol == adminRol.IdRol))
        {
            await context.UsuarioRoles.AddAsync(new UsuarioRol
            {
                IdUsuario = adminUsuario.IdUsuario,
                IdRol = adminRol.IdRol
            });
            await context.SaveChangesAsync();
        }

        var requiredCategories = new[]
        {
            new CategoriaEquipo { Nombre = "Instrumentación", Descripcion = "Equipos de medición y control" },
            new CategoriaEquipo { Nombre = "Equipos de Laboratorio", Descripcion = "Equipos analíticos y de ensayo" },
            new CategoriaEquipo { Nombre = "Equipos de Producción", Descripcion = "Maquinaria de línea de producción" },
            new CategoriaEquipo { Nombre = "Servicios Industriales", Descripcion = "Compresores, calderas, HVAC" }
        };
        var existingCategoryNames = await context.CategoriasEquipo.Select(c => c.Nombre).ToListAsync();
        var missingCategories = requiredCategories
            .Where(required => !existingCategoryNames.Contains(required.Nombre, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missingCategories.Count > 0)
        {
            await context.CategoriasEquipo.AddRangeAsync(missingCategories);
            await context.SaveChangesAsync();
        }

        var requiredLocations = new[]
        {
            new Ubicacion { Planta = "Planta Principal", Area = "Producción", Descripcion = "Área de manufactura" },
            new Ubicacion { Planta = "Planta Principal", Area = "Control de Calidad", Descripcion = "Laboratorio de QC" },
            new Ubicacion { Planta = "Planta Principal", Area = "Almacén", Descripcion = "Bodega de insumos y producto terminado" }
        };
        var existingLocations = await context.Ubicaciones
            .Select(u => new { u.Planta, u.Area })
            .ToListAsync();
        var missingLocations = requiredLocations
            .Where(required => !existingLocations.Any(existing =>
                string.Equals(existing.Planta, required.Planta, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Area, required.Area, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (missingLocations.Count > 0)
        {
            await context.Ubicaciones.AddRangeAsync(missingLocations);
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
