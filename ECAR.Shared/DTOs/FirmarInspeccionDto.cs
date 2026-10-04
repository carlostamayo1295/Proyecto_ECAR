using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Entrada de POST /api/inspecciones/{id}/firmar. Firmar y cerrar son la misma acción.
/// Solo viajan la firma y una observación de cierre: respuestas, evidencias e inspector ya
/// están en el servidor, y cuanto menos viaje, menos puede alterar el cliente.
/// </summary>
public class FirmarInspeccionDto
{
    /// <summary>
    /// PNG de la firma manuscrita en base64, <b>sin</b> el prefijo "data:image/png;base64,".
    /// El límite equivale a unos 200 KB de imagen (base64 ocupa 4/3 del binario).
    /// </summary>
    [Required(ErrorMessage = "La firma es requerida")]
    [MaxLength(280_000, ErrorMessage = "La firma no puede exceder 200 KB")]
    public string FirmaPngBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Observación general del cierre. Las observaciones por pregunta viajan en las respuestas.
    /// </summary>
    public string? Observaciones { get; set; }
}
