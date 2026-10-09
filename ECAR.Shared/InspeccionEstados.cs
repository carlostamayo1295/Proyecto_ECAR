namespace ECAR.Shared;

/// <summary>
/// Estados del ciclo de vida de una inspección. Una inspección nace <see cref="EnCurso"/> y
/// solo pasa a <see cref="Cerrada"/> al firmarse; a partir de ahí es evidencia histórica
/// inmutable (regla 6 del SRS).
/// </summary>
public static class InspeccionEstados
{
    /// <summary>La inspección se está ejecutando: admite respuestas, evidencias y cambios.</summary>
    public const string EnCurso = "EnCurso";

    /// <summary>La inspección fue firmada y cerrada: no admite ninguna modificación.</summary>
    public const string Cerrada = "Cerrada";

    /// <summary>
    /// Fase 4: una inspección en curso que se descarta con motivo en lugar de borrarse
    /// (PLAN_FASE4_TAREAS §3.6). BE-1 la añade a EsValido cuando publique POST {id}/anular.
    /// </summary>
    public const string Anulada = "Anulada";

    public static bool EsValido(string estado) => estado is EnCurso or Cerrada;
}

/// <summary>
/// Resultado que el servidor calcula al cerrar la inspección, según haya o no novedades.
/// </summary>
public static class InspeccionResultados
{
    /// <summary>Ninguna respuesta reportó novedad.</summary>
    public const string Conforme = "Conforme";

    /// <summary>Al menos una respuesta Sí/No se marcó como "No".</summary>
    public const string ConNovedad = "ConNovedad";

    public static bool EsValido(string resultado) => resultado is Conforme or ConNovedad;
}
