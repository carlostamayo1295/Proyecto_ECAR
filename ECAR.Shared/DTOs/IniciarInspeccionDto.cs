using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Entrada de POST /api/inspecciones/iniciar.
/// No lleva inspector ni fecha: el API toma el usuario del token (regla 2 del SRS) y
/// sella la fecha en el servidor.
/// </summary>
public class IniciarInspeccionDto
{
    [Range(1, long.MaxValue, ErrorMessage = "El equipo es requerido")]
    public long IdEquipo { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El checklist es requerido")]
    public long IdChecklist { get; set; }
}
