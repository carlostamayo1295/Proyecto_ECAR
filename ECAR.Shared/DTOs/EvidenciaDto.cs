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

}

