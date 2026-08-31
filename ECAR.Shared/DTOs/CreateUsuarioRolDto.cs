using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

public class CreateUsuarioRolDto
{
    [Required(ErrorMessage = "El usuario es requerido")]
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un usuario válido")]
    public long IdUsuario { get; set; }

    [Required(ErrorMessage = "El rol es requerido")]
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un rol válido")]
    public long IdRol { get; set; }
}
