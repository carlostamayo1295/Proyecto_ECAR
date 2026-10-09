using System.ComponentModel.DataAnnotations;

namespace ECAR.Shared.DTOs;

/// <summary>
/// Cuerpo de las acciones que exigen motivo (PLAN_FASE4_TAREAS §3.3): anular una inspección,
/// un hallazgo o retirar una evidencia; restablecer una contraseña; desbloquear una cuenta.
/// El motivo queda en la fila de auditoría de la acción.
/// </summary>
public class MotivoDto
{
    public const int LongitudMinima = 10;
    public const int LongitudMaxima = 500;

    [Required(ErrorMessage = "El motivo es requerido")]
    [StringLength(LongitudMaxima, MinimumLength = LongitudMinima,
        ErrorMessage = "El motivo debe tener entre 10 y 500 caracteres")]
    public string Motivo { get; set; } = string.Empty;
}
