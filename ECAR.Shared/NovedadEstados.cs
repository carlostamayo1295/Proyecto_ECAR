namespace ECAR.Shared;

/// <summary>
/// Ciclo de vida de una novedad. Nace <see cref="Pendiente"/> al firmar la inspección, una por cada
/// respuesta "No" (regla 4 del SRS), y pasa a <see cref="Resuelta"/> cuando un Técnico o un
/// Administrador firma lo que se hizo, aunque no sea quien inspeccionó. Solo el Administrador
/// puede anular, con motivo, una novedad registrada por error. Resuelta y Anulada son finales: una
/// novedad nunca se borra ni se reabre; si la falla vuelve, sale en la próxima inspección.
/// </summary>
public static class NovedadEstados
{
    public const string Pendiente = "Pendiente";
    public const string Resuelta = "Resuelta";
    public const string Anulada = "Anulada";

    public static readonly IReadOnlyList<string> Todos = [Pendiente, Resuelta, Anulada];

    public static bool EsValido(string? estado) => estado is Pendiente or Resuelta or Anulada;
}
