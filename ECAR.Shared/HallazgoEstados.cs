namespace ECAR.Shared;

/// <summary>
/// Ciclo de vida de un hallazgo (PLAN_FASE4_TAREAS §3.5):
/// Abierto → EnProceso → Cerrado, Cerrado → Abierto (reabrir, solo Administrador) y
/// Abierto/EnProceso → Anulado (solo Administrador). Un hallazgo nunca se borra.
/// </summary>
public static class HallazgoEstados
{
    public const string Abierto = "Abierto";
    public const string EnProceso = "EnProceso";
    public const string Cerrado = "Cerrado";
    public const string Anulado = "Anulado";

    public static readonly IReadOnlyList<string> Todos = [Abierto, EnProceso, Cerrado, Anulado];

    public static bool EsValido(string? estado) => estado is Abierto or EnProceso or Cerrado or Anulado;
}

/// <summary>De dónde viene un hallazgo.</summary>
public static class HallazgoOrigenes
{
    /// <summary>Creado por el servidor al firmar, uno por cada respuesta "No" (regla 4).</summary>
    public const string Novedad = "Novedad";

    /// <summary>Registrado a mano por un Técnico o un Administrador.</summary>
    public const string Manual = "Manual";
}
