using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class EvidenciaDto
{
    public long IdEvidencia { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    public string Archivo { get; set; } = string.Empty;
    public string NombreOriginal { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public DateTime FechaCarga { get; set; }
    public long IdUsuarioCarga { get; set; }
    public string UsuarioCarga { get; set; } = string.Empty;
}

public class CreateEvidenciaDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "El archivo es requerido")]
    public string Archivo { get; set; } = string.Empty;

    // Se conserva temporalmente para no romper el cliente actual; el API toma el usuario del JWT.
    public string? UsuarioCarga { get; set; }
}
