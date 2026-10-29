using ECAR.Shared;
using MudBlazor;

namespace ECAR.Client.Services;

/// <summary>
/// Colores comunes de estados, resultados y criticidad, para que listados, detalles y reportes
/// pinten igual una inspección o una novedad. Los textos están en ECAR.Shared
/// (InspeccionEstados.Texto, InspeccionResultados.Texto).
/// </summary>
public static class FormatoEstados
{
    /// <summary>En curso naranja, con novedades pendientes rojo, cerrada verde, anulada gris.</summary>
    public static Color ColorInspeccion(string? estado) => estado switch
    {
        InspeccionEstados.EnCurso => Color.Warning,
        InspeccionEstados.NovedadesPendientes => Color.Error,
        InspeccionEstados.Cerrada => Color.Success,
        _ => Color.Default
    };

    /// <summary>Satisfactorio y conforme verde, pendiente rojo, con novedad (ya resuelta) naranja.</summary>
    public static Color ColorResultado(string? resultado) => (resultado ?? string.Empty).ToLowerInvariant() switch
    {
        // "Satisfactoria" y "Con novedad" son de las inspecciones de la Fase 1-2.
        "satisfactorio" or "conforme" or "satisfactoria" => Color.Success,
        "pendiente" => Color.Error,
        "connovedad" or "con novedad" => Color.Warning,
        _ => Color.Default
    };

    /// <summary>Pendiente rojo, resuelta verde, anulada gris.</summary>
    public static Color ColorNovedad(string? estado) => estado switch
    {
        NovedadEstados.Pendiente => Color.Error,
        NovedadEstados.Resuelta => Color.Success,
        _ => Color.Default
    };

    public static Color ColorCriticidad(string? criticidad) => criticidad switch
    {
        Criticidades.Alta => Color.Error,
        Criticidades.Media => Color.Warning,
        Criticidades.Baja => Color.Info,
        _ => Color.Default
    };

    /// <summary>Las fechas se guardan en UTC y se muestran en hora de Colombia (UTC−5).</summary>
    public static string HoraColombia(DateTime utc, string formato = "dd/MM/yyyy HH:mm") =>
        DateTime.SpecifyKind(utc, DateTimeKind.Unspecified).AddHours(-5).ToString(formato);
}
