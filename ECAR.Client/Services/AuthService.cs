using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Json;

namespace ECAR.Client.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "auth_token";

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Inicia sesión y guarda el token. Nunca devuelve null: si no se pudo entrar, Success es
    /// false y Message dice por qué, tal cual para mostrarlo en el login (endpoint 22, Fase 4):
    /// el mensaje del servidor si lo manda (p. ej. "Cuenta bloqueada hasta HH:mm" con 423) o, sin
    /// cuerpo, uno según el código. Si entra, Data trae además DebeCambiarPassword y PasswordExpiraEl.
    /// </summary>
    public async Task<ApiResponse<LoginResponseDto>> LoginConMensajeAsync(LoginDto loginDto)
    {
        ApiResponse<LoginResponseDto> respuesta;
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);
            respuesta = await LectorRespuestaApi.LeerAsync<LoginResponseDto>(response, MensajeLoginSegunEstado);
            if (!respuesta.Success)
            {
                Console.WriteLine($"Login error: {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login exception: {ex.Message}");
            return ApiResponse<LoginResponseDto>.ErrorResponse(LectorRespuestaApi.MensajeSinConexion);
        }

        if (!respuesta.Success)
        {
            return respuesta;
        }

        if (respuesta.Data == null || string.IsNullOrEmpty(respuesta.Data.Token))
        {
            return ApiResponse<LoginResponseDto>.ErrorResponse("El servidor no devolvió la sesión. Intente de nuevo.");
        }

        await SaveTokenAsync(respuesta.Data.Token);
        return respuesta;
    }

    /// <summary>Como siempre: los datos de la sesión, o null si no se pudo entrar.</summary>
    public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto) =>
        (await LoginConMensajeAsync(loginDto)) is { Success: true } respuesta ? respuesta.Data : null;

    // En el login, un 401 sin cuerpo es usuario o contraseña incorrectos, no una sesión vencida.
    private static string MensajeLoginSegunEstado(HttpStatusCode estado) => estado == HttpStatusCode.Unauthorized
        ? "Usuario o contraseña incorrectos."
        : LectorRespuestaApi.MensajeSegunEstado(estado);

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveTokenAsync(string token)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving token: {ex.Message}");
        }
    }

    public async Task ClearTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing token: {ex.Message}");
        }
    }

    public async Task<bool> IsAuthenticated()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}