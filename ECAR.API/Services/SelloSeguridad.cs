using System.Security.Claims;
using ECAR.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Services;

/// <summary>
/// Revocación de tokens (endpoint 27, Parte 11 §11.300(c)). Cada token lleva el SecurityStamp
/// del usuario al iniciar sesión; ECARDbContext lo renueva al desactivarlo o cambiar su
/// contraseña, y desde ese momento sus tokens anteriores reciben 401.
/// </summary>
/// <remarks>
/// Sin caché: es una lectura por clave primaria en cada petición autenticada, y así la
/// revocación es inmediata, que es lo que pide §11.300(c). El cliente trata el 401 como sesión
/// vencida y vuelve al login (SesionExpiradaHandler).
/// </remarks>
public static class SelloSeguridad
{
    public const string TipoClaim = "ecar_sello";

    public static async Task ValidarTokenAsync(TokenValidatedContext context)
    {
        var db = context.HttpContext.RequestServices.GetRequiredService<ECARDbContext>();
        if (!await EsVigenteAsync(db, context.Principal, context.HttpContext.RequestAborted))
        {
            context.Fail("La sesión ya no es válida: el usuario fue desactivado o cambió su contraseña");
        }
    }

    /// <summary>
    /// El token es de un usuario activo y su sello coincide con el actual. Un token emitido antes
    /// de la Fase 4 no trae sello y tampoco vale: basta con volver a iniciar sesión.
    /// </summary>
    public static async Task<bool> EsVigenteAsync(ECARDbContext db, ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default)
    {
        var sello = principal?.FindFirstValue(TipoClaim);
        if (string.IsNullOrEmpty(sello) ||
            !long.TryParse(principal!.FindFirstValue(ClaimTypes.NameIdentifier), out var idUsuario))
        {
            return false;
        }

        var usuario = await db.Usuarios.AsNoTracking()
            .Where(u => u.IdUsuario == idUsuario)
            .Select(u => new { u.Activo, u.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken);

        return usuario is { Activo: true } && string.Equals(usuario.SecurityStamp, sello, StringComparison.Ordinal);
    }
}
