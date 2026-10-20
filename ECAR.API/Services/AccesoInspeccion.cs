using ECAR.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

/// <summary>
/// Qué inspecciones puede consultar un Técnico (decisión de ECAR del 20/10/2026): las suyas y, en
/// solo lectura, las que tienen novedades. Cualquier Técnico puede resolver una novedad, y para
/// orientarse necesita ver la inspección completa de la que sale: respuestas, fotos, firma y
/// registro PDF. Una inspección solo tiene novedades después de firmada, así que la que está en
/// curso sigue siendo solo de quien la ejecuta; modificar tampoco cambia (el inspector o el
/// Administrador). El Administrador y el Auditor consultan cualquiera. Inspecciones, respuestas y
/// evidencias preguntan aquí para que la regla viva en un solo sitio.
/// </summary>
public static class AccesoInspeccion
{
    /// <summary>
    /// Si el Técnico <paramref name="idUsuario"/> puede leer la inspección
    /// <paramref name="idInspeccion"/>, ejecutada por <paramref name="idInspector"/>.
    /// </summary>
    public static async Task<bool> TecnicoPuedeLeerAsync(
        this ECARDbContext context, long idInspeccion, long idInspector, long idUsuario) =>
        idInspector == idUsuario
        || await context.Novedades.AnyAsync(novedad => novedad.IdInspeccion == idInspeccion);
}
