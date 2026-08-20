using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("Checklists")]
public class Checklist
{
    [Key]
    [Column("IdChecklist")]
    public long IdChecklist { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("Version")]
    public string Version { get; set; } = string.Empty;

    [Column("Activo")]
    public bool Activo { get; set; } = true;

    [Column("FechaCreacion")]
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<PreguntaChecklist> Preguntas { get; set; } = new List<PreguntaChecklist>();
    public virtual ICollection<Inspeccion> Inspecciones { get; set; } = new List<Inspeccion>();
}