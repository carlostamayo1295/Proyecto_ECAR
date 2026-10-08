using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class RetirarEvidenciaDto
{
    [Required(ErrorMessage = "El motivo de retiro es obligatorio.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "El motivo debe tener entre 10 y 500 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}