using ECAR.Shared.DTOs;

namespace ECAR.API.Services;

public interface IAuthService
{
    /// <summary>Los datos de la sesión, o null si no se pudo entrar (por cualquier motivo).</summary>
    Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);

    /// <summary>Inicio de sesión con bloqueo por intentos y auditoría (endpoint 22).</summary>
    Task<ResultadoLogin> IniciarSesionAsync(LoginDto loginDto);

    /// <summary>Reautenticación al firmar o al cambiar la contraseña (endpoints 12 y 23).</summary>
    Task<ResultadoCredencial> VerificarPasswordAsync(long idUsuario, string? password, string contexto);

    Task<bool> ValidateTokenAsync(string token);
}

public enum EstadoCredencial
{
    Valida,

    /// <summary>Usuario o contraseña incorrectos: 401 en el login, 400 al firmar.</summary>
    Invalida,

    /// <summary>Cuenta bloqueada por intentos fallidos: 423.</summary>
    Bloqueada
}

/// <param name="Local">La contraseña era la local (BCrypt) y no la de Active Directory.</param>
public sealed record ResultadoCredencial(EstadoCredencial Estado, DateTime? BloqueadoHasta = null, bool Local = false)
{
    /// <summary>"Cuenta bloqueada hasta 14:35 (hora de Colombia, UTC−5)…", el texto del 423.</summary>
    public string MensajeBloqueo => MensajesCuenta.Bloqueada(BloqueadoHasta);
}

public sealed record ResultadoLogin(EstadoCredencial Estado, LoginResponseDto? Sesion, DateTime? BloqueadoHasta)
{
    public string MensajeBloqueo => MensajesCuenta.Bloqueada(BloqueadoHasta);
}

public static class MensajesCuenta
{
    /// <summary>Genérico a propósito: no dice si el usuario existe (§11.300(d)).</summary>
    public const string CredencialesIncorrectas = "Usuario o contraseña incorrectos";

    public static string Bloqueada(DateTime? hasta) => hasta is { } h
        ? $"Cuenta bloqueada hasta las {HoraColombia.Texto(h, "HH:mm")} ({HoraColombia.Zona}) por varios intentos fallidos. " +
          "Inténtelo de nuevo a esa hora o pida al administrador que la desbloquee."
        : "La cuenta está bloqueada por varios intentos fallidos.";
}
