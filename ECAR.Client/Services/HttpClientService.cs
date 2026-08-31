using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECAR.Client.Services;

public class HttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;

    public HttpClientService(HttpClient httpClient, AuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    private async Task AddAuthorizationHeaderAsync()
    {
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task RemoveAuthorizationHeaderAsync()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    // Usuarios API Methods
    public async Task<ApiResponse<PagedResultDto<UsuarioDto>>?> GetUsuariosAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            
            var query = $"api/usuarios?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<UsuarioDto>>>();
            
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting usuarios: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioDto>?> GetUsuarioAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/usuarios/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting usuario: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioDto>?> CreateUsuarioAsync(CreateUsuarioDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/usuarios", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating usuario: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioDto>?> UpdateUsuarioAsync(long id, UpdateUsuarioDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/usuarios/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating usuario: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteUsuarioAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/usuarios/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting usuario: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Roles API Methods
    public async Task<ApiResponse<PagedResultDto<RolDto>>?> GetRolesAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            
            var query = $"api/roles?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<RolDto>>>();
            
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting roles: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<RolDto>?> GetRolAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/roles/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<RolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting rol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<RolDto>?> CreateRolAsync(CreateRolDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/roles", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<RolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating rol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<RolDto>?> UpdateRolAsync(long id, UpdateRolDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/roles/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<RolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating rol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteRolAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/roles/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting rol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Equipos API Methods
    public async Task<ApiResponse<PagedResultDto<EquipoDto>>?> GetEquiposAsync(int page = 1, int pageSize = 10, string? search = null, string? criticidad = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/equipos?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }
            if (!string.IsNullOrEmpty(criticidad))
            {
                query += $"&criticidad={Uri.EscapeDataString(criticidad)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<EquipoDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting equipos: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EquipoDto>?> GetEquipoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/equipos/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EquipoDto>?> CreateEquipoAsync(CreateEquipoDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/equipos", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EquipoDto>?> UpdateEquipoAsync(long id, UpdateEquipoDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/equipos/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteEquipoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/equipos/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<LookupDto>>?> GetCategoriasEquipoAsync()
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync("api/equipos/categorias");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<LookupDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting categorias: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<LookupDto>>?> GetUbicacionesEquipoAsync()
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync("api/equipos/ubicaciones");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<LookupDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting ubicaciones: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // UsuariosRol (Asignación de Roles) API Methods
    public async Task<ApiResponse<PagedResultDto<UsuarioRolDto>>?> GetUsuariosRolAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/usuariosrol?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<UsuarioRolDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting usuariosRol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioRolDto>?> GetUsuarioRolAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/usuariosrol/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioRolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting usuarioRol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioRolDto>?> CreateUsuarioRolAsync(CreateUsuarioRolDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/usuariosrol", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioRolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating usuarioRol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UsuarioRolDto>?> UpdateUsuarioRolAsync(long id, UpdateUsuarioRolDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/usuariosrol/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UsuarioRolDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating usuarioRol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteUsuarioRolAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/usuariosrol/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting usuarioRol: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<LookupDto>>?> GetUsuariosLookupAsync()
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync("api/usuariosrol/usuarios");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<LookupDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting usuarios lookup: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<LookupDto>>?> GetRolesLookupAsync()
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync("api/usuariosrol/roles");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<LookupDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting roles lookup: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }
}