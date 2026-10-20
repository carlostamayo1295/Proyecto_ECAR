namespace ECAR.Shared;

/// <summary>
/// Estados del ciclo de vida de una inspección. Una inspección nace <see cref="EnCurso"/> y al
/// firmarse pasa a <see cref="Cerrada"/> o, si reportó novedades, a <see cref="NovedadesPendientes"/>
/// hasta que se resuelva la última. Firmada, es evidencia histórica inmutable (regla 6 del SRS).
/// </summary>
public static class InspeccionEstados
{
    /// <summary>La inspección se está ejecutando: admite respuestas, evidencias y cambios.</summary>
    public const string EnCurso = "EnCurso";

    /// <summary>
    /// La inspección se firmó con novedades y alguna sigue sin resolver. Como la cerrada, ya no
    /// admite ninguna modificación, pero no se da por terminada: pasa sola a <see cref="Cerrada"/>
    /// cuando se resuelve (o se anula) su última novedad pendiente.
    /// </summary>
    public const string NovedadesPendientes = "NovedadesPendientes";

    /// <summary>La inspección fue firmada y no tiene novedades pendientes: no admite ninguna modificación.</summary>
    public const string Cerrada = "Cerrada";

    /// <summary>
    /// Fase 4: una inspección en curso que se descarta con motivo en lugar de borrarse
    /// (PLAN_FASE4_TAREAS §3.6). Conserva todos sus datos y, como la cerrada, ya no admite cambios.
    /// </summary>
    public const string Anulada = "Anulada";

    public static bool EsValido(string estado) => estado is EnCurso or NovedadesPendientes or Cerrada or Anulada;

    /// <summary>Firmada: cerrada o con novedades pendientes. Las dos son inmutables y tienen resultado firmado.</summary>
    public static bool EstaFirmada(string? estado) => estado is Cerrada or NovedadesPendientes;

    /// <summary>Texto para pantalla: "En curso" y "Novedades pendientes" en lugar de los valores internos.</summary>
    public static string Texto(string? estado) => estado switch
    {
        null or "" => "-",
        EnCurso => "En curso",
        NovedadesPendientes => "Novedades pendientes",
        _ => estado
    };
}

/// <summary>
/// Resultado que el servidor calcula al firmar la inspección y, si tuvo novedades, al resolverse la última.
/// </summary>
public static class InspeccionResultados
{
    /// <summary>
    /// Ninguna respuesta reportó novedad, o todas sus novedades ya se resolvieron (o se anularon).
    /// Que hubo novedades queda en las propias novedades y en TotalNovedades.
    /// </summary>
    public const string Conforme = "Conforme";

    /// <summary>
    /// La inspección se firmó con novedades y todavía no se resuelven todas. Pasa a
    /// <see cref="Conforme"/> cuando se resuelve la última.
    /// </summary>
    public const string Pendiente = "Pendiente";

    /// <summary>
    /// Inspecciones firmadas antes de las novedades: al menos una respuesta Sí/No fue "No". Ya no lo
    /// pone el servidor, pero se conserva para leer esos registros.
    /// </summary>
    public const string ConNovedad = "ConNovedad";

    public static bool EsValido(string resultado) => resultado is Conforme or Pendiente or ConNovedad;

    /// <summary>Texto para pantalla: "Con novedad" en lugar del valor interno "ConNovedad".</summary>
    public static string Texto(string? resultado) => resultado switch
    {
        null or "" => "Sin registrar",
        ConNovedad => "Con novedad",
        _ => resultado
    };
}
