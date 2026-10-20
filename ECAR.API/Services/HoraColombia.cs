using System.Globalization;

namespace ECAR.API.Services;

/// <summary>
/// El servidor guarda todo en UTC; los filtros por fecha y los documentos usan la hora de
/// Colombia (UTC−5, sin horario de verano), que es la de ECAR.
/// </summary>
public static class HoraColombia
{
    public static readonly TimeSpan Desfase = TimeSpan.FromHours(-5);
    public const string Zona = "hora de Colombia, UTC−5";

    public static DateTime DesdeUtc(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Unspecified).Add(Desfase);

    /// <summary>"16/10/2026 14:35" en hora de Colombia.</summary>
    public static string Texto(DateTime utc, string formato = "dd/MM/yyyy HH:mm") =>
        DesdeUtc(utc).ToString(formato, CultureInfo.InvariantCulture);

    /// <summary>
    /// Un día local "desde" (incluido) y "hasta" (incluido) como rango UTC [inicio, fin): el
    /// cliente manda solo la fecha (yyyy-MM-dd) y el día va de 00:00 a 23:59 en Colombia.
    /// </summary>
    public static (DateTime? Inicio, DateTime? Fin) RangoUtc(DateTime? desde, DateTime? hasta) =>
        (desde?.Date - Desfase, hasta?.Date.AddDays(1) - Desfase);
}
