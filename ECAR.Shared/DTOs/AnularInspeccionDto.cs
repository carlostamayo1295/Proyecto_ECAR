using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class AnularInspeccionDto
{
    [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
    [MaxLength(500, ErrorMessage = "El motivo no puede exceder los 500 caracteres.")]
    public string MotivoAnulacion { get; set; } = string.Empty;
}