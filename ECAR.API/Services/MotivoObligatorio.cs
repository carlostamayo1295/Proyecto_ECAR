using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

/// <summary>
/// Regla de §3.3 (PLAN_FASE4_TAREAS): modificar un equipo o un checklist y desactivar o reactivar
/// cualquier registro exige un motivo de 10 a 500 caracteres, que queda en la auditoría. Donde el
/// motivo no viaja en un MotivoDto (PUT con su DTO, DELETE y activar sin cuerpo) se valida aquí.
/// </summary>
public static class MotivoObligatorio
{
    public static string? Validar(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < MotivoDto.LongitudMinima ||
        motivo.Trim().Length > MotivoDto.LongitudMaxima
            ? $"El motivo es obligatorio y debe tener entre {MotivoDto.LongitudMinima} y {MotivoDto.LongitudMaxima} caracteres"
            : null;
}
