using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    // Guardar token en localStorage
                    await SaveTokenAsync(apiResponse.Data.Token);
                    return apiResponse.Data;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Login error: {response.StatusCode} - {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login exception: {ex.Message}");
        }

        return null;
    }

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