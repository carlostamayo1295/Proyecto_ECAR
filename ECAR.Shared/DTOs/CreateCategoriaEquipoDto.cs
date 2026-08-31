using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateCategoriaEquipoDto
{
    [Required(ErrorMessage = "El nombre de la categoría es requerido")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Descripcion { get; set; }
}
