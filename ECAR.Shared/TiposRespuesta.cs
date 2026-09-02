namespace ECAR.Shared;

/// <summary>Tipo de respuesta admitido por las preguntas de un checklist.</summary>
/// <param name="Valor">Valor que se guarda en PreguntasChecklist.TipoRespuesta.</param>
/// <param name="Etiqueta">Etiqueta que se muestra al usuario.</param>
public record TipoRespuestaOpcion(string Valor, string Etiqueta);

/// <summary>
/// Catálogo de los tipos de respuesta que puede usar una pregunta de checklist. Es compartido
/// para que el API y el cliente coincidan en los valores guardados y no dependan de texto libre.
/// </summary>
public static class TiposRespuesta
{
    /// <summary>Dos casillas ("Sí" / "No") en las que el inspector marca la correcta.</summary>
    public const string SiNo = "SiNo";

    /// <summary>Texto libre que rellena el inspector.</summary>
    public const string Texto = "Texto";

    public static readonly IReadOnlyList<TipoRespuestaOpcion> Todos =
    [
        new(SiNo, "Sí / No (dos casillas)"),
        new(Texto, "Rellenar información")
    ];

    public static bool EsValido(string? tipo) =>
        Todos.Any(t => t.Valor == tipo);

    /// <summary>Etiqueta de un valor guardado; devuelve el valor crudo para registros antiguos.</summary>
    public static string Etiqueta(string? tipo) =>
        Todos.FirstOrDefault(t => t.Valor == tipo)?.Etiqueta ?? tipo ?? string.Empty;

    public static string ValoresPermitidos =>
        string.Join(", ", Todos.Select(t => t.Valor));
}
