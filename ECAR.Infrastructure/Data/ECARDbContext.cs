using ECAR.Infrastructure.Entities;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;

namespace ECAR.Infrastructure.Data;

public partial class ECARDbContext : DbContext
{
    /// <summary>
    /// <paramref name="contextoAuditoria"/> dice quién, desde dónde y por qué se hace cada cambio
    /// (en el API, el usuario del token y la petición HTTP). Sin él, la auditoría se escribe a
    /// nombre de "Sistema", que es lo que pasa en las pruebas y al sembrar datos.
    /// </summary>
    public ECARDbContext(DbContextOptions<ECARDbContext> options, IContextoAuditoria? contextoAuditoria = null)
        : base(options)
    {
        _contextoAuditoria = contextoAuditoria;
    }

    // Conjuntos de entidades (DbSets)
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
    public DbSet<HistorialPassword> HistorialPasswords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Equipo
        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.HasIndex(e => e.CodigoInterno).IsUnique();
            entity.HasIndex(e => e.ActivoFijo).IsUnique();
            entity.HasIndex(e => e.IdCategoria);
            entity.HasIndex(e => e.IdUbicacion);
            entity.HasIndex(e => e.Criticidad);
            // La consulta pública por QR busca por token: único y filtrado para ignorar los equipos sin QR
            entity.HasIndex(e => e.QRCode).IsUnique().HasFilter("[QRCode] IS NOT NULL");
        });

        // Configuración de CategoriaEquipo
        modelBuilder.Entity<CategoriaEquipo>(entity =>
        {
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de Ubicacion
        modelBuilder.Entity<Ubicacion>(entity =>
        {
            entity.HasIndex(e => new { e.Planta, e.Area }).IsUnique();
        });

        // Configuración de Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.Correo).IsUnique();
            entity.HasIndex(e => e.UsuarioAD).IsUnique();
        });

        // Configuración de HistorialPassword (Fase 4)
        modelBuilder.Entity<HistorialPassword>(entity =>
        {
            entity.HasIndex(e => new { e.IdUsuario, e.Fecha });
            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.HistorialPasswords)
                .HasForeignKey(e => e.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuración de Rol
        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasIndex(e => e.Nombre).IsUnique();
        });

        // Configuración de UsuarioRol
        modelBuilder.Entity<UsuarioRol>(entity =>
        {
            entity.HasIndex(e => new { e.IdUsuario, e.IdRol }).IsUnique();
        });

        // Configuración de Checklist
        modelBuilder.Entity<Checklist>(entity =>
        {
            entity.HasIndex(e => new { e.Nombre, e.Version }).IsUnique();
            entity.HasIndex(e => e.Activo);
        });

        // Configuración de PreguntaChecklist
        modelBuilder.Entity<PreguntaChecklist>(entity =>
        {
            entity.HasIndex(e => e.IdChecklist);
            entity.HasIndex(e => e.TipoRespuesta);
        });

        // Configuración de Inspeccion
        modelBuilder.Entity<Inspeccion>(entity =>
        {
            entity.Property(e => e.Estado)
                .HasDefaultValue(InspeccionEstados.EnCurso);
            entity.HasIndex(e => e.IdEquipo);
            entity.HasIndex(e => e.IdUsuario);
            entity.HasIndex(e => e.IdChecklist);
            entity.HasIndex(e => e.Estado);
            entity.HasIndex(e => new { e.IdUsuario, e.Estado });
            entity.HasIndex(e => e.FechaInspeccion);
            entity.HasIndex(e => e.Resultado);
            entity.HasOne(e => e.Checklist)
                .WithMany(c => c.Inspecciones)
                .HasForeignKey(e => e.IdChecklist)
                .OnDelete(DeleteBehavior.Restrict);
            // Dos relaciones con Usuarios: hay que decir cuál es la de Usuario.Inspecciones.
            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Inspecciones)
                .HasForeignKey(e => e.IdUsuario)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.UsuarioAnulacion)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioAnulacion)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de RespuestaInspeccion
        modelBuilder.Entity<RespuestaInspeccion>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.IdPregunta);
            entity.HasIndex(e => new { e.IdInspeccion, e.IdPregunta }).IsUnique();
        });

        // Configuración de Evidencia
        modelBuilder.Entity<Evidencia>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.IdUsuarioCarga);
            entity.HasIndex(e => e.FechaCarga);

            entity.HasOne(e => e.Inspeccion)
                .WithMany(i => i.Evidencias)
                .HasForeignKey(e => e.IdInspeccion)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UsuarioCargaDetalle)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioCarga)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.UsuarioRetiro)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioRetiro)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Hallazgo
        modelBuilder.Entity<Hallazgo>(entity =>
        {
            entity.HasIndex(e => e.IdInspeccion);
            entity.HasIndex(e => e.Criticidad);
            entity.HasIndex(e => e.Estado);
            entity.HasIndex(e => e.FechaRegistro);
            entity.Property(e => e.Estado).HasDefaultValue(HallazgoEstados.Abierto);
            entity.Property(e => e.Origen).HasDefaultValue(HallazgoOrigenes.Manual);

            // Las relaciones con usuarios y preguntas no borran en cascada: un hallazgo se conserva siempre.
            entity.HasOne(e => e.Pregunta)
                .WithMany()
                .HasForeignKey(e => e.IdPregunta)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioRegistro)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioRegistro)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioResponsable)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioResponsable)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.UsuarioCierre)
                .WithMany()
                .HasForeignKey(e => e.IdUsuarioCierre)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de Auditoria
        modelBuilder.Entity<Auditoria>(entity =>
        {
            entity.HasIndex(e => e.Tabla);
            entity.HasIndex(e => e.RegistroId);
            entity.HasIndex(e => e.Accion);
            entity.HasIndex(e => e.Usuario);
            entity.HasIndex(e => e.FechaHora);
            entity.HasIndex(e => new { e.Tabla, e.RegistroId, e.FechaHora });

            // El trigger lo crea la migración Fase4HallazgosAuditoria. Declararlo hace que EF no
            // use OUTPUT sin INTO al insertar, que SQL Server no permite en tablas con triggers.
            entity.ToTable(tabla => tabla.HasTrigger(TriggerAuditoriaSoloInsercion));

            entity.HasOne<Usuario>()
                .WithMany()
                .HasForeignKey(e => e.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
