using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class IniciarInspeccionDto
{
    [Range(1, long.MaxValue, ErrorMessage = "El equipo es requerido")]
    public long IdEquipo { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El checklist es requerido")]
    public long IdChecklist { get; set; }
}

public class InspeccionEjecucionDto
{
    public long IdInspeccion { get; set; }
    public long IdEquipo { get; set; }
    public string CodigoInternoEquipo { get; set; } = string.Empty;
    public string NombreEquipo { get; set; } = string.Empty;
    public long IdChecklist { get; set; }
    public string NombreChecklist { get; set; } = string.Empty;
    public string VersionChecklist { get; set; } = string.Empty;
    public long IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public DateTime FechaInspeccion { get; set; }
    public string Estado { get; set; } = InspeccionEstados.EnCurso;
    public List<PreguntaEjecucionDto> Preguntas { get; set; } = [];
    public List<EvidenciaDto> Evidencias { get; set; } = [];
}

public class PreguntaEjecucionDto
{
    public long IdPregunta { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string TipoRespuesta { get; set; } = string.Empty;
    public bool Obligatoria { get; set; }
    public int Orden { get; set; }
    public long? IdRespuesta { get; set; }
    public string? Respuesta { get; set; }
    public string? Observacion { get; set; }
}
