namespace ECAR.Shared;

/// <summary>
/// Sede de ECAR en la que se hizo la inspección (decisión de ECAR, 29/10/2026). Se elige en el
/// formulario de cada inspección y es obligatoria para firmarla.
/// </summary>
public static class Sedes
{
    public const string Rionegro = "Rionegro";
    public const string Medellin = "Medellín";

    public static readonly IReadOnlyList<string> Todas = [Rionegro, Medellin];

    /// <summary>La sede tal como se guarda, o null si no es una de las de ECAR (sin distinguir mayúsculas).</summary>
    public static string? Normalizar(string? sede) =>
        Todas.FirstOrDefault(s => string.Equals(s, sede?.Trim(), StringComparison.OrdinalIgnoreCase));
}
