namespace ECAR.Shared;

/// <summary>
/// Acciones que se escriben en la tabla Auditoria (Fase 4, PLAN_FASE4_TAREAS §3.3). Backend
/// las escribe y el visor las filtra y las colorea, así que ambos usan estas constantes.
/// </summary>
public static class AuditoriaAcciones
{
    // Cambios de datos: los escribe la auditoría transaccional de ECARDbContext.
    public const string Crear = "Crear";
    public const string Modificar = "Modificar";
    public const string Eliminar = "Eliminar";
    public const string Desactivar = "Desactivar";
    public const string Activar = "Activar";
    public const string Anular = "Anular";
    public const string Cerrar = "Cerrar";
    public const string Firmar = "Firmar";

    /// <summary>Un hallazgo cerrado que el Administrador vuelve a abrir, con motivo (§3.5).</summary>
    public const string Reabrir = "Reabrir";

    /// <summary>Una evidencia quitada de una inspección en curso: se conserva, no se borra (§3.6).</summary>
    public const string Retirar = "Retirar";

    // Eventos sin entidad (Tabla = "Sistema").
    public const string LoginExitoso = "LoginExitoso";
    public const string LoginFallido = "LoginFallido";
    public const string CuentaBloqueada = "CuentaBloqueada";
    public const string CierreSesion = "CierreSesion";
    public const string CambioPassword = "CambioPassword";
    public const string RestablecerPassword = "RestablecerPassword";
    public const string Exportacion = "Exportacion";
    public const string VerificacionIntegridad = "VerificacionIntegridad";

    public static readonly IReadOnlyList<string> Todas =
    [
        Crear, Modificar, Eliminar, Desactivar, Activar, Anular, Cerrar, Firmar, Reabrir, Retirar,
        LoginExitoso, LoginFallido, CuentaBloqueada, CierreSesion, CambioPassword,
        RestablecerPassword, Exportacion, VerificacionIntegridad
    ];
}

/// <summary>
/// Valores de Auditoria.Tabla: el nombre de la tabla de SQL Server ([Table] de cada entidad),
/// y "Sistema" para los eventos que no cambian ningún registro (inicios de sesión, exportaciones).
/// </summary>
public static class AuditoriaTablas
{
    public const string Sistema = "Sistema";

    public static readonly IReadOnlyList<string> Todas =
    [
        "Equipos", "CategoriasEquipo", "Ubicaciones", "Checklists", "PreguntasChecklist",
        "Inspecciones", "RespuestasInspeccion", "Evidencias", "Hallazgos",
        "Usuarios", "Roles", "UsuarioRol", "HistorialPasswords", Sistema
    ];
}
