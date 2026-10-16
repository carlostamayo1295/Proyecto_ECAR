using System.Security.Cryptography;
using BCrypt.Net;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Entities;

namespace ECAR.API.Services;

/// <summary>
/// Política de contraseñas locales (PLAN_FASE4_TAREAS §3.8, Parte 11 §11.300(b)): longitud mínima,
/// letras y números, caducidad e historial. Las de Active Directory las gobierna el directorio.
/// </summary>
public static class PoliticaPassword
{
    public static List<string> Validar(string? password, SeguridadOptions opciones)
    {
        var errores = new List<string>();
        if (string.IsNullOrEmpty(password) || password.Length < opciones.LongitudMinimaPassword)
        {
            errores.Add($"La contraseña debe tener al menos {opciones.LongitudMinimaPassword} caracteres");
        }

        if (string.IsNullOrEmpty(password) || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            errores.Add("La contraseña debe combinar letras y números");
        }

        return errores;
    }

    /// <summary>La nueva contraseña coincide con la actual o con una de las últimas recordadas.</summary>
    public static bool EstaRepetida(string password, Usuario usuario, SeguridadOptions opciones)
    {
        var anteriores = usuario.HistorialPasswords
            .OrderByDescending(h => h.Fecha)
            .Take(Math.Max(opciones.PasswordsRecordadas - 1, 0))
            .Select(h => h.PasswordHash)
            .Prepend(usuario.PasswordHash);

        return anteriores.Any(hash => CoincideSinExcepcion(password, hash));
    }

    public static bool CoincideSinExcepcion(string password, string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (SaltParseException)
        {
            // Un hash antiguo dañado no coincide con nada.
            return false;
        }
    }

    /// <summary>Caduca a los N días del último cambio; null si no hay fecha de cambio todavía.</summary>
    public static DateTime? ExpiraEl(Usuario usuario, SeguridadOptions opciones) =>
        usuario.FechaCambioPassword?.AddDays(opciones.DiasExpiracionPassword);

    /// <summary>
    /// Contraseña temporal para restablecer (endpoint 25): 14 caracteres con mayúsculas,
    /// minúsculas, números y un símbolo, sin caracteres que se confundan al dictarla (0/O, 1/l).
    /// </summary>
    public static string GenerarTemporal()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnpqrstuvwxyz";
        const string numeros = "23456789";
        const string simbolos = "!#%*+?";
        var todos = mayusculas + minusculas + numeros + simbolos;

        var caracteres = new List<char>
        {
            mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)],
            minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
            simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)]
        };
        while (caracteres.Count < 14)
        {
            caracteres.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);
        }

        // Fisher–Yates para que los obligatorios no queden siempre al principio.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }
}
