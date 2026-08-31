using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECAR.Infrastructure.Data;

public class ECARDbContext : DbContext
{
    public ECARDbContext(DbContextOptions<ECARDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<Equipo> Equipos { get; set; }
    public DbSet<CategoriaEquipo> CategoriasEquipo { get; set; }
    public DbSet<Ubicacion> Ubicaciones { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Rol> Roles { get; set; }
    public DbSet<UsuarioRol> UsuarioRoles { get; set; }
    public DbSet<Checklist> Checklists { get; set; }
    public DbSet<PreguntaChecklist> PreguntasChecklist { get; set; }
    public DbSet<Inspeccion> Inspecciones { get; set; }
    public DbSet<RespuestaInspeccion> RespuestasInspeccion { get; set; }
    public DbSet<Evidencia> Evidencias { get; set; }
    public DbSet<Hallazgo> Hallazgos { get; set; }
    public DbSet<Auditoria> Auditoria { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Equipo
        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.HasIndex(e => e.CodigoInterno).IsUnique();
            entity.HasIndex(e => e.ActivoFijo).IsUnique();
            entity.HasIndex(e => e.IdCategoria);
            entity.HasIndex(e => e.IdUbicacion);
            entity.HasIndex(e => e.Criticidad);
        });

        // Configure CategoriaEquipo
        modelBuilder.Entity<CategoriaEquipo>(entity =>
        {
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configure Ubicacion
        modelBuilder.Entity<Ubicacion>(entity =>
        {
            entity.HasIndex(e => new { e.Planta, e.Area }).IsUnique();
        });

        // Configure Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.Correo).IsUnique();
            entity.HasIndex(e => e.UsuarioAD).IsUnique();
            entity.HasIndex(e => e.Correo); // Índice adicional para búsqueda rápida en login
        });

        // Configure Rol
        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configure UsuarioRol
        modelBuilder.Entity<UsuarioRol>(entity =>
        {
            entity.HasIndex(e => new { e.IdUsuario, e.IdRol }).IsUnique();
        });

        // Configure Checklist
        modelBuilder.Entity<Checklist>(entity =>
        {
            entity.HasIndex(e => new { e.Nombre, e.Version }).IsUnique();
            entity.HasIndex(e => e.Activo);
        });

        // Configure PreguntaChecklist
        modelBuilder.Entity<PreguntaChecklist>(entity =>
        {
            entity.HasIndex(e => e.IdChecklist);
            entity.HasIndex(e => e.TipoRespuesta);
        });

        // Configure Inspeccion
        modelBuilder.Entity<Inspeccion>(entity =>
        {
            entity.HasIndex(e => e.IdEquipo);
            entity.HasIndex(e => e.IdUsuario);
            entity.HasIndex(e => e.FechaInspeccion);
            entity.HasIndex(e => e.Resultado);
        });

        // Configure RespuestaInspeccion
        modelBuilder.Entity<RespuestaInspeccion>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.IdPregunta);
            entity.HasIndex(e => new { e.IdInspeccion, e.IdPregunta }).IsUnique();
        });

        // Configure Evidencia
        modelBuilder.Entity<Evidencia>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.UsuarioCarga);
            entity.HasIndex(e => e.FechaCarga);

            entity.HasOne(e => e.Inspeccion)
                .WithMany(i => i.Evidencias)
                .HasForeignKey(e => e.IdInspeccion)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Hallazgo
        modelBuilder.Entity<Hallazgo>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.Criticidad);
            entity.HasIndex(e => e.Estado);
            entity.HasIndex(e => e.FechaRegistro);
        });

        // Configure Auditoria
        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.HasIndex(e => e.Tabla);
            entity.HasIndex(e => e.RegistroId);
            entity.HasIndex(e => e.Accion);
            entity.HasIndex(e => e.Usuario);
            entity.HasIndex(e => e.FechaHora);
            entity.HasIndex(e => new { e.Tabla, e.RegistroId, e.FechaHora });
        });
    }
}