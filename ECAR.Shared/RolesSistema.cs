namespace ECAR.Shared;

/// <summary>
/// Roles del sistema (decisión de ECAR del 21/10/2026). Los usan el API, en
/// <c>[Authorize(Roles = ...)]</c> y en las comprobaciones de cada acción, y el cliente, para el menú
/// y las pantallas. Ninguno pierde datos al renombrarse: el rol se guarda por Id.
/// <list type="bullet">
/// <item><b>Administrador</b>: todos los permisos.</item>
/// <item><b>Operador</b> (antes Técnico): ejecuta sus inspecciones y consulta equipos y checklists; de
/// las novedades solo ve las de sus inspecciones, sin resolverlas. Sin reportes ni auditoría.</item>
/// <item><b>Estándar</b>: ejecuta inspecciones y resuelve novedades; ve todas las novedades y todas
/// las evidencias, las haya creado quien las haya creado, y para resolver consulta, en solo lectura,
/// la inspección completa de otro. Sin reportes ni auditoría.</item>
/// <item><b>Supervisor</b> (antes Auditor): consulta todo, incluidos reportes, auditoría e
/// integridad, y no modifica nada.</item>
/// </list>
/// </summary>
public static class RolesSistema
{
    public const string Administrador = "Administrador";
    public const string Operador = "Operador";
    public const string Estandar = "Estándar";
    public const string Supervisor = "Supervisor";

    /// <summary>Nombres anteriores: al arrancar, el API renombra los de una base existente.</summary>
    public const string TecnicoAnterior = "Técnico";
    public const string AuditorAnterior = "Auditor";

    // Listas para [Authorize(Roles = ...)]: deben ser constantes.

    /// <summary>Cualquier usuario con rol: consultas de catálogos, tablero, inspecciones y novedades.</summary>
    public const string Todos = Administrador + "," + Operador + "," + Estandar + "," + Supervisor;

    /// <summary>Ejecutan inspecciones: iniciar, responder, subir fotos, firmar y anular la suya.</summary>
    public const string Inspeccionan = Administrador + "," + Operador + "," + Estandar;

    /// <summary>Inspectores sin lectura global (sus inspecciones; el Estándar, también las ajenas con novedades).</summary>
    public const string Inspectores = Operador + "," + Estandar;

    /// <summary>Resuelven novedades con firma.</summary>
    public const string Resuelven = Administrador + "," + Estandar;

    /// <summary>Reportes, auditoría y verificación de integridad.</summary>
    public const string Supervisan = Administrador + "," + Supervisor;

    public static readonly IReadOnlyList<string> DelSistema = [Administrador, Operador, Estandar, Supervisor];
}
