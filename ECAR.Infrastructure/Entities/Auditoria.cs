using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Auditoria")]
public class Auditoria
{
    [Key]
    [Column("IdAuditoria")]
    public long IdAuditoria { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Tabla")]
    public string Tabla { get; set; } = string.Empty;

    [Required]
    [Column("RegistroId")]
    public long RegistroId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("Accion")]
    public string Accion { get; set; } = string.Empty;

    [Column("ValorAnterior")]
    public string? ValorAnterior { get; set; }

    [Column("ValorNuevo")]
    public string? ValorNuevo { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("Usuario")]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [Column("FechaHora")]
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    // Fase 4 (PLAN_FASE4_TAREAS §3.1 y §3.3). Las filas las escribe solo ECARDbContext al guardar.

    /// <summary>Quién hizo el cambio; null en los procesos del sistema y en los intentos de inicio de sesión.</summary>
    [Column("IdUsuario")]
    public long? IdUsuario { get; set; }

    [MaxLength(500)]
    [Column("Motivo")]
    public string? Motivo { get; set; }

    [MaxLength(45)]
    [Column("DireccionIp")]
    public string? DireccionIp { get; set; }

    [MaxLength(300)]
    [Column("AgenteUsuario")]
    public string? AgenteUsuario { get; set; }

    /// <summary>Hash de la fila anterior (null solo en la primera fila de la cadena).</summary>
    [Column("HashAnterior", TypeName = "char(64)")]
    public string? HashAnterior { get; set; }

    /// <summary>SHA-256 de la fila encadenado con el anterior; ver CadenaAuditoria.</summary>
    [Required]
    [Column("Hash", TypeName = "char(64)")]
    public string Hash { get; set; } = string.Empty;
}