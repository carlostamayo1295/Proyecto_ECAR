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
}