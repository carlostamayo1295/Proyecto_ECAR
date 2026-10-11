namespace ECAR.Shared;

/// <summary>
/// Cada cuánto se debe inspeccionar un equipo (Equipos.FrecuenciaInspeccion, PLAN_FASE4_TAREAS
/// §3.1). El reporte de cumplimiento cuenta los equipos programados a partir de ella. Pendiente
/// de confirmar con ECAR; un equipo sin frecuencia no entra en el cumplimiento.
/// </summary>
public static class FrecuenciasInspeccion
{
    public const string Diaria = "Diaria";
    public const string Semanal = "Semanal";
    public const string Mensual = "Mensual";
    public const string Trimestral = "Trimestral";

    public static readonly IReadOnlyList<string> Todas = [Diaria, Semanal, Mensual, Trimestral];

    public static bool EsValida(string? frecuencia) => frecuencia is Diaria or Semanal or Mensual or Trimestral;
}
