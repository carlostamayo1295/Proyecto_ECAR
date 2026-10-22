using ECAR.Infrastructure.Data;
using ECAR.Shared;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

/// <summary>
/// Qué inspecciones puede consultar cada rol (<see cref="RolesSistema"/>). El Administrador y el
/// Supervisor, todas. El Operador y el Estándar, las suyas; el Estándar además, en solo lectura, las
/// que tienen novedades: las resuelve aunque no las haya hecho, y para orientarse necesita ver la
/// inspección completa (respuestas, fotos, firma y registro PDF; decisión de ECAR del 20/10/2026).
/// Una inspección solo tiene novedades después de firmada, así que la que está en curso sigue
/// siendo solo de quien la ejecuta; modificar tampoco cambia (el inspector o el Administrador).
/// Inspecciones, respuestas y evidencias preguntan aquí para que la regla viva en un solo sitio.
/// </summary>
public static class AccesoInspeccion
{
    /// <summary>Administrador o Supervisor: consultan cualquier inspección.</summary>
    public static bool LeeTodas(ICurrentUser usuario) =>
        usuario.IsInRole(RolesSistema.Administrador) || usuario.IsInRole(RolesSistema.Supervisor);

    /// <summary>El Estándar ve también las inspecciones ajenas que tienen novedades.</summary>
    public static bool VeAjenasConNovedades(ICurrentUser usuario) => usuario.IsInRole(RolesSistema.Estandar);

    /// <summary>Si <paramref name="usuario"/> puede leer la inspección, ejecutada por <paramref name="idInspector"/>.</summary>
    public static async Task<bool> PuedeLeerAsync(
        this ECARDbContext context, ICurrentUser usuario, long idInspeccion, long idInspector) =>
        LeeTodas(usuario)
        || ((usuario.IsInRole(RolesSistema.Operador) || usuario.IsInRole(RolesSistema.Estandar))
            && await context.InspectorPuedeLeerAsync(idInspeccion, idInspector, usuario.IdUsuario, VeAjenasConNovedades(usuario)));

    /// <summary>
    /// Regla de un Operador o un Estándar: la suya o, si <paramref name="veAjenasConNovedades"/>, una
    /// ajena que tenga novedades.
    /// </summary>
    public static async Task<bool> InspectorPuedeLeerAsync(
        this ECARDbContext context, long idInspeccion, long idInspector, long idUsuario, bool veAjenasConNovedades) =>
        idInspector == idUsuario
        || (veAjenasConNovedades && await context.Novedades.AnyAsync(novedad => novedad.IdInspeccion == idInspeccion));
}
