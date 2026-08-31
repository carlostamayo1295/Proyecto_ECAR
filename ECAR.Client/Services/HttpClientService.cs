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

    // Categorías de Equipo API Methods
    public async Task<ApiResponse<PagedResultDto<CategoriaEquipoDto>>?> GetCategoriasEquipoAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/categoriasequipo?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<CategoriaEquipoDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting categorias de equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<CategoriaEquipoDto>?> GetCategoriaEquipoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/categoriasequipo/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<CategoriaEquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting categoria de equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<CategoriaEquipoDto>?> CreateCategoriaEquipoAsync(CreateCategoriaEquipoDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/categoriasequipo", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<CategoriaEquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating categoria de equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<CategoriaEquipoDto>?> UpdateCategoriaEquipoAsync(long id, UpdateCategoriaEquipoDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/categoriasequipo/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<CategoriaEquipoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating categoria de equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteCategoriaEquipoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/categoriasequipo/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting categoria de equipo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Checklists API Methods
    public async Task<ApiResponse<PagedResultDto<ChecklistDto>>?> GetChecklistsAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/checklists?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<ChecklistDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting checklists: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<ChecklistDto>?> GetChecklistAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/checklists/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<ChecklistDto>?> CreateChecklistAsync(CreateChecklistDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/checklists", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<ChecklistDto>?> UpdateChecklistAsync(long id, UpdateChecklistDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/checklists/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteChecklistAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/checklists/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Auditoría API Methods
    public async Task<ApiResponse<PagedResultDto<AuditoriaDto>>?> GetAuditoriaAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/auditoria?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<AuditoriaDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting auditoria: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Equipos API Methods
    public async Task<ApiResponse<PagedResultDto<EquipoDto>>?> GetEquiposAsync(int page = 1, int pageSize = 100, string? search = null, string? criticidad = null)
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

    public async Task<ApiResponse<List<LookupDto>>?> GetEquipoCategoriasLookupAsync()
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
            Console.WriteLine($"Error getting categorias lookup: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<LookupDto>>?> GetEquipoUbicacionesLookupAsync()
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
            Console.WriteLine($"Error getting ubicaciones lookup: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Ubicaciones API Methods
    public async Task<ApiResponse<PagedResultDto<UbicacionDto>>?> GetUbicacionesAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/ubicaciones?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<UbicacionDto>>>();

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

    public async Task<ApiResponse<UbicacionDto>?> CreateUbicacionAsync(CreateUbicacionDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/ubicaciones", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UbicacionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating ubicacion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<UbicacionDto>?> UpdateUbicacionAsync(long id, UpdateUbicacionDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/ubicaciones/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UbicacionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating ubicacion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteUbicacionAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/ubicaciones/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting ubicacion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Usuarios-Rol (asignaciones) API Methods
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
            Console.WriteLine($"Error getting usuarios-rol: {ex.Message}");
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
            Console.WriteLine($"Error creating usuario-rol: {ex.Message}");
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
            Console.WriteLine($"Error updating usuario-rol: {ex.Message}");
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
            Console.WriteLine($"Error deleting usuario-rol: {ex.Message}");
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

    // Inspecciones API Methods
    public async Task<ApiResponse<PagedResultDto<InspeccionDto>>?> GetInspeccionesAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/inspecciones?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<InspeccionDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting inspecciones: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<InspeccionDto>?> GetInspeccionAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/inspecciones/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<InspeccionDto>?> CreateInspeccionAsync(CreateInspeccionDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/inspecciones", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<InspeccionDto>?> UpdateInspeccionAsync(long id, UpdateInspeccionDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/inspecciones/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteInspeccionAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/inspecciones/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Evidencias API Methods
    public async Task<ApiResponse<PagedResultDto<EvidenciaDto>>?> GetEvidenciasAsync(int page = 1, int pageSize = 10, string? search = null, long? idInspeccion = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/evidencias?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }
            if (idInspeccion.HasValue)
            {
                query += $"&idInspeccion={idInspeccion.Value}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<EvidenciaDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting evidencias: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EvidenciaDto>?> GetEvidenciaAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/evidencias/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EvidenciaDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting evidencia: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EvidenciaDto>?> CreateEvidenciaAsync(CreateEvidenciaDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/evidencias", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EvidenciaDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating evidencia: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteEvidenciaAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/evidencias/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting evidencia: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Hallazgos API Methods
    public async Task<ApiResponse<PagedResultDto<HallazgoDto>>?> GetHallazgosAsync(int page = 1, int pageSize = 10, string? search = null, long? idInspeccion = null, string? estado = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/hallazgos?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }
            if (idInspeccion.HasValue)
            {
                query += $"&idInspeccion={idInspeccion.Value}";
            }
            if (!string.IsNullOrEmpty(estado))
            {
                query += $"&estado={Uri.EscapeDataString(estado)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<HallazgoDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting hallazgos: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<HallazgoDto>?> GetHallazgoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/hallazgos/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<HallazgoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting hallazgo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<HallazgoDto>?> CreateHallazgoAsync(CreateHallazgoDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/hallazgos", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<HallazgoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating hallazgo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<HallazgoDto>?> UpdateHallazgoAsync(long id, UpdateHallazgoDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/hallazgos/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<HallazgoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating hallazgo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeleteHallazgoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/hallazgos/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting hallazgo: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }
}