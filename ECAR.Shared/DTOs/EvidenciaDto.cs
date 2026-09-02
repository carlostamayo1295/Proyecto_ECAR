using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class EvidenciaDto
{
    public long IdEvidencia { get; set; }
    public long IdInspeccion { get; set; }
    public string? NombreEquipo { get; set; }
    public string Archivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public string UsuarioCarga { get; set; } = string.Empty;
}

public class CreateEvidenciaDto
{
    [Required(ErrorMessage = "La inspección es requerida")]
    public long IdInspeccion { get; set; }

    [Required(ErrorMessage = "El archivo es requerido")]
    public string Archivo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El usuario que carga la evidencia es requerido")]
    [MaxLength(100, ErrorMessage = "El usuario no puede exceder 100 caracteres")]
    public string UsuarioCarga { get; set; } = string.Empty;
}
