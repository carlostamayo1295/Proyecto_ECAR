using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class EvidenciaDto
{
    public long IdEvidencia { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    /// <summary>Ruta relativa en el almacenamiento; no se muestra en pantalla.</summary>
    public string Archivo { get; set; } = string.Empty;

    public string NombreOriginal { get; set; } = string.Empty;   // foto_balanza.jpg
    public string TipoContenido { get; set; } = string.Empty;    // image/jpeg
    public long TamanoBytes { get; set; }
    public DateTime FechaCarga { get; set; }
    public long IdUsuarioCarga { get; set; }
    public string UsuarioCarga { get; set; } = string.Empty;

    /// <summary>
    /// Estado de la inspección a la que pertenece esta evidencia ("EnCurso" o "Cerrada").
    /// </summary>
    public string EstadoInspeccion { get; set; } = string.Empty;

    // --- Fase 4: una foto quitada antes de firmar se marca como retirada y se conserva ---
    public bool Retirada { get; set; }
    public DateTime? FechaRetiro { get; set; }
    public string? MotivoRetiro { get; set; }
    public string? UsuarioRetiro { get; set; }
}

// Contrato temporal para que el cliente anterior a FE-2 siga compilando mientras
// la carga real de fotografías usa multipart en /api/inspecciones/{id}/evidencias.
public class CreateEvidenciaDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "El archivo es requerido")]
    public string Archivo { get; set; } = string.Empty;

    public string? UsuarioCarga { get; set; }
}

