using System.Security.Cryptography;
using BCrypt.Net;
using ECAR.API.Configuration;
using ECAR.Infrastructure.Data;
using ECAR.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECAR.API.Services;

public class PoliticaPasswords(IOptions<SeguridadOptions> opciones)
{
    private const string Letras = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digitos = "23456789";
    private const int LongitudTemporal = 14;

    public SeguridadOptions Opciones => opciones.Value;

    public List<string> Validar(string? password)
    {
        var errores = new List<string>();
        password ??= string.Empty;

        if (password.Length < Opciones.LongitudMinimaPassword)
        {
            errores.Add($"La contraseña debe tener al menos {Opciones.LongitudMinimaPassword} caracteres");
        }

        if (Opciones.RequiereLetrasYNumeros && (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)))
        {
            errores.Add("La contraseña debe combinar letras y números");
        }

        return errores;
    }

    public async Task<bool> EsRecienteAsync(ECARDbContext context, Usuario usuario, string password)
    {
        if (CoincideConHash(password, usuario.PasswordHash))
        {
            return true;
        }

        var anteriores = await context.HistorialPasswords
            .AsNoTracking()
            .Where(h => h.IdUsuario == usuario.IdUsuario)
            .OrderByDescending(h => h.Fecha)
            .ThenByDescending(h => h.Id)
            .Take(Math.Max(0, Opciones.PasswordsRecordadas))
            .Select(h => h.PasswordHash)
            .ToListAsync();

        return anteriores.Any(hash => CoincideConHash(password, hash));
    }

    public void Establecer(ECARDbContext context, Usuario usuario, string password, bool esTemporal)
    {
        if (!string.IsNullOrWhiteSpace(usuario.PasswordHash))
        {
            context.HistorialPasswords.Add(new HistorialPassword
            {
                IdUsuario = usuario.IdUsuario,
                PasswordHash = usuario.PasswordHash,
                Fecha = DateTime.UtcNow
            });
        }

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        usuario.FechaCambioPassword = DateTime.UtcNow;
        usuario.DebeCambiarPassword = esTemporal;
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;
    }

    public DateTime? CalcularExpiracion(Usuario usuario)
    {
        if (string.IsNullOrWhiteSpace(usuario.PasswordHash) || Opciones.DiasExpiracionPassword <= 0)
        {
            return null;
        }

        return usuario.FechaCambioPassword?.AddDays(Opciones.DiasExpiracionPassword);
    }

    public bool EstaCaducada(Usuario usuario) =>
        CalcularExpiracion(usuario) is { } expira && expira <= DateTime.UtcNow;

    public string GenerarTemporal()
    {
        var longitud = Math.Max(LongitudTemporal, Opciones.LongitudMinimaPassword);
        var caracteres = new char[longitud];
        caracteres[0] = Letras[RandomNumberGenerator.GetInt32(Letras.Length)];
        caracteres[1] = Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)];

        var alfabeto = Letras + Digitos;
        for (var i = 2; i < longitud; i++)
        {
            caracteres[i] = alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
        }

        RandomNumberGenerator.Shuffle(caracteres.AsSpan());
        return new string(caracteres);
    }

    private static bool CoincideConHash(string password, string? hash)
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
            return false;
        }
    }
}
