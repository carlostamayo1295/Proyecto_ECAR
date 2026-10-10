namespace ECAR.API.Services;

/// <summary>
/// Motivo del cambio que se está guardando en esta petición (PLAN_FASE4_TAREAS §3.3). El
/// controlador lo fija con el <c>motivo</c> del DTO antes de SaveChangesAsync, y la auditoría lo
/// copia en cada fila. Es obligatorio en: anular (inspección, hallazgo, evidencia), cambiar el
/// estado de un hallazgo, reabrir, modificar un equipo o un checklist, desactivar o reactivar,
/// restablecer una contraseña y desbloquear una cuenta.
/// </summary>
public interface IMotivoCambio
{
    string? Motivo { get; }

    void Establecer(string? motivo);
}

public sealed class MotivoCambio : IMotivoCambio
{
    public string? Motivo { get; private set; }

    public void Establecer(string? motivo) =>
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
}
