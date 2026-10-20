using ECAR.Shared;
using MudBlazor;

namespace ECAR.Client.Services;

/// <summary>Presentación común de la auditoría: color de cada acción y hora local.</summary>
public static class FormatoAuditoria
{
    public static Color ColorAccion(string? accion) => accion switch
    {
        AuditoriaAcciones.Crear or AuditoriaAcciones.Activar or AuditoriaAcciones.LoginExitoso => Color.Success,
        AuditoriaAcciones.Modificar or AuditoriaAcciones.Reabrir or AuditoriaAcciones.CambioPassword => Color.Info,
        AuditoriaAcciones.Firmar or AuditoriaAcciones.Cerrar => Color.Primary,
        AuditoriaAcciones.Desactivar or AuditoriaAcciones.Retirar or AuditoriaAcciones.RestablecerPassword => Color.Warning,
        AuditoriaAcciones.Eliminar or AuditoriaAcciones.Anular or AuditoriaAcciones.LoginFallido
            or AuditoriaAcciones.CuentaBloqueada => Color.Error,
        // Filas anteriores a la Fase 4.
        _ => (accion ?? string.Empty).ToUpperInvariant() switch
        {
            "INSERT" or "CREATE" => Color.Success,
            "UPDATE" or "EDITAR" => Color.Info,
            "DELETE" or "ELIMINAR" => Color.Error,
            _ => Color.Default
        }
    };

    /// <summary>La auditoría guarda la hora del servidor en UTC; se muestra en la hora local con segundos.</summary>
    public static string FechaHora(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
