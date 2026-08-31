using BCrypt.Net;
using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECAR.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedDataAsync(ECARDbContext context, IConfiguration configuration)
    {
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

        // Datos base para el módulo de Equipos
        if (!await context.CategoriasEquipo.AnyAsync())
        {
            await context.CategoriasEquipo.AddRangeAsync(
                new CategoriaEquipo { Nombre = "Instrumentación", Descripcion = "Equipos de medición y control" },
                new CategoriaEquipo { Nombre = "Equipos de Laboratorio", Descripcion = "Equipos analíticos y de ensayo" },
                new CategoriaEquipo { Nombre = "Equipos de Producción", Descripcion = "Maquinaria de línea de producción" },
                new CategoriaEquipo { Nombre = "Servicios Industriales", Descripcion = "Compresores, calderas, HVAC" });
            await context.SaveChangesAsync();
        }

        if (!await context.Ubicaciones.AnyAsync())
        {
            await context.Ubicaciones.AddRangeAsync(
                new Ubicacion { Planta = "Planta Principal", Area = "Producción", Descripcion = "Área de manufactura" },
                new Ubicacion { Planta = "Planta Principal", Area = "Control de Calidad", Descripcion = "Laboratorio de QC" },
                new Ubicacion { Planta = "Planta Principal", Area = "Almacén", Descripcion = "Bodega de insumos y producto terminado" });
            await context.SaveChangesAsync();
        }
    }
}