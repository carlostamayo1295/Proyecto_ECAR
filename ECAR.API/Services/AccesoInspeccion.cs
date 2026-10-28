using ECAR.Shared;

namespace ECAR.API.Services;

/// <summary>
/// Qué inspecciones puede consultar cada rol (<see cref="RolesSistema"/>), con sus respuestas, fotos,
/// firma y registro PDF. El Administrador, el Supervisor y el Estándar, todas, las haya hecho quien
/// las haya hecho (el Estándar desde el 28/10/2026; antes veía las suyas y, para orientarse al
/// resolver novedades, las ajenas que las tenían). El Operador, solo las suyas. Consultar no es
/// modificar: eso sigue siendo del inspector o del Administrador, y firmar, solo del inspector.
/// Inspecciones, respuestas y evidencias preguntan aquí para que la regla viva en un solo sitio.
/// </summary>
public static class AccesoInspeccion
{
    /// <summary>Administrador, Supervisor o Estándar: consultan cualquier inspección.</summary>
    public static bool LeeTodas(ICurrentUser usuario) =>
        usuario.IsInRole(RolesSistema.Administrador) || usuario.IsInRole(RolesSistema.Supervisor)
        || usuario.IsInRole(RolesSistema.Estandar);

    /// <summary>Si <paramref name="usuario"/> puede leer la inspección, ejecutada por <paramref name="idInspector"/>.</summary>
    public static bool PuedeLeer(ICurrentUser usuario, long idInspector) =>
        LeeTodas(usuario) || (usuario.IsInRole(RolesSistema.Operador) && idInspector == usuario.IdUsuario);
}
