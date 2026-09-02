using BCrypt.Net;
using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECAR.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(ECARDbContext context, IConfiguration configuration)
    {
        // Se siembra cada grupo por separado para poder completar sin riesgo una base parcialmente poblada.
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
}
