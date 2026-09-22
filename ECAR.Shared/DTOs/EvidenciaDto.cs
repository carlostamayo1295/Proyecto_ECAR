using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class EvidenciaDto
{
    public long IdEvidencia { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    public string NombreOriginal { get; set; } = string.Empty; // Foto.jpg
    public string TipoContenido { get; set; } = string.Empty; // image/jpeg
    public long TamanoBytes { get; set; }
    public string Archivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public long IdUsuarioCarga { get; set; }
    public string UsuarioCarga { get; set; } = string.Empty;
}

// Contrato de transición: la carga real de fotografías es multipart
// (POST /api/inspecciones/{id}/evidencias, BE-2) y no usa este DTO.
// Se conserva mientras la pantalla de Evidencias siga registrando una referencia de texto;
// se elimina cuando FE-2 migre `EvidenciaModal` al componente de cámara.
public class CreateEvidenciaDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "El archivo es requerido")]
    public string Archivo { get; set; } = string.Empty;

    // El API toma el usuario del token; se acepta por compatibilidad y se ignora.
    public string? UsuarioCarga { get; set; }
}

