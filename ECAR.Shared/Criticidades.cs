namespace ECAR.Shared;

/// <summary>
/// Clasificación por criticidad de un equipo y de un hallazgo (SRS §2). Los filtros, los
/// hallazgos generados (heredan la del equipo) y los reportes agrupan por estos valores, así que
/// el API no admite texto libre.
/// </summary>
public static class Criticidades
{
    public const string Alta = "Alta";
    public const string Media = "Media";
    public const string Baja = "Baja";

    public static readonly IReadOnlyList<string> Todas = [Alta, Media, Baja];

    public const string Mensaje = "La criticidad debe ser Alta, Media o Baja";

    /// <summary>Vacía también vale: la criticidad es opcional.</summary>
    public static bool EsValida(string? criticidad) =>
        string.IsNullOrWhiteSpace(criticidad) || criticidad.Trim() is Alta or Media or Baja;
}
