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

    /// <summary>Una novedad que un Técnico o un Administrador resuelve con su firma.</summary>
    public const string Resolver = "Resolver";

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

    /// <summary>Salió el correo de una novedad a la mesa de ayuda (RegistroId = la novedad).</summary>
    public const string NotificacionEnviada = "NotificacionEnviada";

    /// <summary>No se pudo enviar el correo de una novedad; el Administrador puede reenviarlo.</summary>
    public const string NotificacionFallida = "NotificacionFallida";

    public static readonly IReadOnlyList<string> Todas =
    [
        Crear, Modificar, Eliminar, Desactivar, Activar, Anular, Cerrar, Firmar, Resolver, Retirar,
        LoginExitoso, LoginFallido, CuentaBloqueada, CierreSesion, CambioPassword,
        RestablecerPassword, Exportacion, VerificacionIntegridad, NotificacionEnviada, NotificacionFallida
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
        "Inspecciones", "RespuestasInspeccion", "Evidencias", "Novedades",
        "Usuarios", "Roles", "UsuarioRol", "HistorialPasswords", Sistema
    ];
}
