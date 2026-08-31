using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECAR.Infrastructure.Entities;

[Table("PreguntasChecklist")]
public class PreguntaChecklist
{
    [Key]
    [Column("IdPregunta")]
    public long IdPregunta { get; set; }

    [Required]
    [Column("IdChecklist")]
    public long IdChecklist { get; set; }

    [Required]
    [Column("Pregunta")]
    public string Pregunta { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("TipoRespuesta")]
    public string TipoRespuesta { get; set; } = string.Empty;

    [Column("Obligatoria")]
    public bool Obligatoria { get; set; } = false;

    // Navigation properties
    [ForeignKey("IdChecklist")]
    public virtual Checklist Checklist { get; set; } = null!;

    public virtual ICollection<RespuestaInspeccion> Respuestas { get; set; } = new List<RespuestaInspeccion>();
}