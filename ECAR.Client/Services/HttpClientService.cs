using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ECAR.Client.Services;

public class HttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly IJSRuntime _jsRuntime;

    public HttpClientService(HttpClient httpClient, AuthService authService, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _authService = authService;
        _jsRuntime = jsRuntime;
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

    // Métodos del API de Usuarios
    public async Task<ApiResponse<PagedResultDto<UsuarioDto>>?> GetUsuariosAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    /// <summary>Reactiva un usuario desactivado (PUT api/usuarios/{id}/activar).</summary>
    public async Task<ApiResponse<bool>?> ActivarUsuarioAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsync($"api/usuarios/{id}/activar", null);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error activando usuario: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Métodos del API de Roles
    public async Task<ApiResponse<PagedResultDto<RolDto>>?> GetRolesAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    // Métodos del API de Categorías de Equipo
    public async Task<ApiResponse<PagedResultDto<CategoriaEquipoDto>>?> GetCategoriasEquipoAsync(int page = 1,
        int pageSize = 10, string? search = null)
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
            var apiResponse =
                await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<CategoriaEquipoDto>>>();

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

    public async Task<ApiResponse<CategoriaEquipoDto>?> UpdateCategoriaEquipoAsync(long id,
        UpdateCategoriaEquipoDto updateDto)
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

    // Métodos del API de Checklists
    public async Task<ApiResponse<PagedResultDto<ChecklistDto>>?> GetChecklistsAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    /// <summary>Reactiva un checklist desactivado (PUT api/checklists/{id}/activar).</summary>
    public async Task<ApiResponse<bool>?> ActivarChecklistAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsync($"api/checklists/{id}/activar", null);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error activando checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<ChecklistDto>?> CreateChecklistVersionAsync(long id, CreateChecklistVersionDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/checklists/{id}/nueva-version", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating checklist version: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<ChecklistVersionDto>>?> GetChecklistVersionesAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/checklists/{id}/versiones");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<ChecklistVersionDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting checklist versiones: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Métodos del API de Preguntas de Checklist
    public async Task<ApiResponse<PagedResultDto<PreguntaChecklistDto>>?> GetPreguntasChecklistAsync(int page = 1, int pageSize = 10, string? search = null, long? idChecklist = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/preguntaschecklist?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }
            if (idChecklist.HasValue)
            {
                query += $"&idChecklist={idChecklist.Value}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<PreguntaChecklistDto>>>();

            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting preguntas checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<PreguntaChecklistDto>?> GetPreguntaChecklistAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/preguntaschecklist/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PreguntaChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting pregunta checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<PreguntaChecklistDto>?> CreatePreguntaChecklistAsync(CreatePreguntaChecklistDto createDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/preguntaschecklist", createDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PreguntaChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating pregunta checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<PreguntaChecklistDto>?> UpdatePreguntaChecklistAsync(long id, UpdatePreguntaChecklistDto updateDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/preguntaschecklist/{id}", updateDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PreguntaChecklistDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating pregunta checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<bool>?> DeletePreguntaChecklistAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.DeleteAsync($"api/preguntaschecklist/{id}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting pregunta checklist: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // Métodos del API de Auditoría
    public async Task<ApiResponse<PagedResultDto<AuditoriaDto>>?> GetAuditoriaAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    // Métodos del API de Equipos
    public async Task<ApiResponse<PagedResultDto<EquipoDto>>?> GetEquiposAsync(int page = 1, int pageSize = 100,
        string? search = null, string? criticidad = null, bool? activo = null, string? planta = null, string? area = null)
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

            if (activo.HasValue)
            {
                query += $"&activo={activo.Value.ToString().ToLowerInvariant()}";
            }

            if (!string.IsNullOrEmpty(planta))
            {
                query += $"&planta={Uri.EscapeDataString(planta)}";
            }

            if (!string.IsNullOrEmpty(area))
            {
                query += $"&area={Uri.EscapeDataString(area)}";
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

    /// <summary>Reactiva un equipo desactivado (PUT api/equipos/{id}/activar).</summary>
    public async Task<ApiResponse<bool>?> ActivarEquipoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsync($"api/equipos/{id}/activar", null);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error activando equipo: {ex.Message}");
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

    public async Task<ApiResponse<EquipoQrDto>?> GenerateEquipoQrAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsync($"api/equipos/{id}/qr", null);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EquipoQrDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating equipo QR: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<EquipoQrDto>?> RegenerateEquipoQrAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsync($"api/equipos/{id}/qr/regenerar", null);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EquipoQrDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error regenerating equipo QR: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // El endpoint exige token, así que un <img src> directo no sirve: se descargan los bytes
    // y la pantalla los muestra como data URI (ver GetEquipoQrImageDataUrlAsync).
    public async Task<byte[]?> GetEquipoQrImageAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/equipos/{id}/qr.png");
            await RemoveAuthorizationHeaderAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting equipo QR image: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<string?> GetEquipoQrImageDataUrlAsync(long id)
    {
        var bytes = await GetEquipoQrImageAsync(id);
        return bytes == null ? null : $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    // Consulta pública: es la que se abre al escanear el QR, sin sesión iniciada.
    public async Task<ApiResponse<ConsultaQrDto>?> GetEquipoByQrAsync(string token)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/equipos/qr/{Uri.EscapeDataString(token)}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<ConsultaQrDto>>();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting equipo by QR: {ex.Message}");
            return null;
        }
    }

    // Métodos del API de Ubicaciones
    public async Task<ApiResponse<PagedResultDto<UbicacionDto>>?> GetUbicacionesAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    // Métodos del API de Usuarios-Rol (asignaciones)
    public async Task<ApiResponse<PagedResultDto<UsuarioRolDto>>?> GetUsuariosRolAsync(int page = 1, int pageSize = 10,
        string? search = null)
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

    // Métodos del API de Inspecciones
    public async Task<ApiResponse<PagedResultDto<InspeccionDto>>?> GetInspeccionesAsync(int page = 1, int pageSize = 10,
        string? search = null, string? estado = null, long? idEquipo = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/inspecciones?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            if (!string.IsNullOrEmpty(estado))
            {
                query += $"&estado={Uri.EscapeDataString(estado)}";
            }

            if (idEquipo.HasValue)
            {
                query += $"&idEquipo={idEquipo.Value}";
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

    // Métodos del API de Evidencias
    public async Task<ApiResponse<PagedResultDto<EvidenciaDto>>?> GetEvidenciasAsync(int page = 1, int pageSize = 10,
        string? search = null, long? idInspeccion = null)
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

    // Métodos del API de Hallazgos
    public async Task<ApiResponse<PagedResultDto<HallazgoDto>>?> GetHallazgosAsync(int page = 1, int pageSize = 10,
        string? search = null, long? idInspeccion = null, string? estado = null)
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

    // Métodos del API de Preguntas de Checklist
    public async Task<ApiResponse<List<PreguntaChecklistDto>>?> GetPreguntasChecklistAsync(long idChecklist)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/PreguntasChecklist/checklist/{idChecklist}");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<PreguntaChecklistDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting preguntas: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // ===================== FASE 3 — EJECUCIÓN DE INSPECCIONES =====================
    // Contrato acordado entre FE-0 y BE-0 (docs/PLAN_FASE3_TAREAS.md §3.2).
    // Todos siguen la convención del archivo: token en la cabecera, ApiResponse<T> y null si la
    // llamada falla; la pantalla siempre debe avisar con Snackbar, nunca dejar un control vacío.

    /// <summary>Tamaño máximo aceptado al leer una fotografía del dispositivo (5 MB).</summary>
    public const long MaxEvidenciaBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Inicia una inspección para un equipo y checklist. Si el técnico ya tiene una inspección
    /// en curso para ese equipo, el API responde 409 y devuelve la existente en Data: la pantalla
    /// debe continuarla en vez de mostrar un error.
    /// </summary>
    public async Task<ApiResponse<InspeccionEjecucionDto>?> IniciarInspeccionAsync(IniciarInspeccionDto iniciarDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync("api/inspecciones/iniciar", iniciarDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionEjecucionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error iniciando inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>
    /// Estado completo de la inspección: cabecera, preguntas con sus respuestas y evidencias.
    /// Es la única llamada que necesita la pantalla de ejecución para dibujarse.
    /// </summary>
    public async Task<ApiResponse<InspeccionEjecucionDto>?> GetInspeccionEjecucionAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/inspecciones/{id}/ejecucion");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionEjecucionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting inspeccion ejecucion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>
    /// Guarda un lote de respuestas (upsert por pregunta). Se puede llamar con una sola respuesta
    /// para el guardado incremental mientras el técnico avanza.
    /// Devuelve el estado completo recalculado por el servidor —incluidos los contadores—,
    /// así que no hace falta volver a pedir la ejecución tras guardar.
    /// </summary>
    public async Task<ApiResponse<InspeccionEjecucionDto>?> GuardarRespuestasAsync(long idInspeccion,
        GuardarRespuestasDto guardarDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/inspecciones/{idInspeccion}/respuestas", guardarDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionEjecucionDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error guardando respuestas: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<RespuestaInspeccionDto>>?> GetRespuestasInspeccionAsync(long idInspeccion)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/inspecciones/{idInspeccion}/respuestas");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<RespuestaInspeccionDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting respuestas inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>Listado paginado para la pantalla de administración de respuestas (solo consulta).</summary>
    public async Task<ApiResponse<PagedResultDto<RespuestaInspeccionDto>>?> GetRespuestasInspeccionAsync(
        int page = 1, int pageSize = 10, string? search = null, long? idInspeccion = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = $"api/respuestasinspeccion?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }
            if (idInspeccion.HasValue)
            {
                query += $"&idInspeccion={idInspeccion.Value}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResultDto<RespuestaInspeccionDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting respuestas: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>Inspecciones del técnico autenticado, opcionalmente filtradas por estado.</summary>
    public async Task<ApiResponse<List<InspeccionDto>>?> GetMisInspeccionesAsync(string? estado = null)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            var query = "api/inspecciones/mias";
            if (!string.IsNullOrEmpty(estado))
            {
                query += $"?estado={Uri.EscapeDataString(estado)}";
            }

            var response = await _httpClient.GetAsync(query);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<InspeccionDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting mis inspecciones: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>
    /// Sube una fotografía como evidencia (multipart). El archivo llega desde MudFileUpload;
    /// conviene comprimirlo antes con RequestImageFileAsync para no agotar la red de planta.
    /// </summary>
    public async Task<ApiResponse<EvidenciaDto>?> UploadEvidenciaAsync(long idInspeccion, IBrowserFile archivo)
    {
        try
        {
            await AddAuthorizationHeaderAsync();

            using var contenido = new MultipartFormDataContent();
            await using var stream = archivo.OpenReadStream(MaxEvidenciaBytes);
            using var archivoContenido = new StreamContent(stream);
            archivoContenido.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(archivo.ContentType);
            contenido.Add(archivoContenido, "archivo", archivo.Name);

            var response = await _httpClient.PostAsync($"api/inspecciones/{idInspeccion}/evidencias", contenido);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<EvidenciaDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subiendo evidencia: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<List<EvidenciaDto>>?> GetEvidenciasInspeccionAsync(long idInspeccion)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/inspecciones/{idInspeccion}/evidencias");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<EvidenciaDto>>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting evidencias inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // El endpoint de la imagen exige token, así que un <img src> directo no sirve: se descargan
    // los bytes y la pantalla los muestra como data URI (mismo patrón que el QR de Fase 2).
    public async Task<byte[]?> GetEvidenciaImageAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/evidencias/{id}/archivo");
            await RemoveAuthorizationHeaderAsync();

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting evidencia image: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    /// <summary>Imagen de la evidencia lista para el atributo Src de MudImage.</summary>
    public async Task<string?> GetEvidenciaImageDataUrlAsync(long id, string tipoContenido = "image/jpeg")
    {
        var bytes = await GetEvidenciaImageAsync(id);
        return bytes == null ? null : $"data:{tipoContenido};base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>
    /// Firma y cierra la inspección. Si falta alguna pregunta obligatoria o una novedad sin
    /// observación, el API responde 400 y detalla los faltantes en Errors.
    /// </summary>
    public async Task<ApiResponse<InspeccionResultadoDto>?> FirmarInspeccionAsync(long id, FirmarInspeccionDto firmarDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PostAsJsonAsync($"api/inspecciones/{id}/firmar", firmarDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionResultadoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error firmando inspeccion: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    public async Task<ApiResponse<InspeccionResultadoDto>?> GetInspeccionResultadoAsync(long id)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.GetAsync($"api/inspecciones/{id}/resultado");
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InspeccionResultadoDto>>();
            await RemoveAuthorizationHeaderAsync();
            return apiResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting inspeccion resultado: {ex.Message}");
            await RemoveAuthorizationHeaderAsync();
            return null;
        }
    }

    // =============================================================================================
    // Fase 4 — hallazgos, auditoría, reportes y Parte 11 (PLAN_FASE4_TAREAS §3.4)
    //
    // Los endpoints se publican a lo largo de la fase. Mientras uno no existe, el API responde 404
    // sin cuerpo; estos métodos nunca devuelven null: devuelven Success = false con un mensaje que
    // se puede mostrar tal cual. El token va en la propia petición, no en DefaultRequestHeaders,
    // para que dos llamadas simultáneas no se pisen la cabecera.
    // =============================================================================================

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private async Task<HttpRequestMessage> CrearPeticionAsync(HttpMethod metodo, string url, object? cuerpo = null)
    {
        var peticion = new HttpRequestMessage(metodo, url);
        var token = await _authService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (cuerpo != null)
        {
            peticion.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson);
        }

        return peticion;
    }

    private async Task<ApiResponse<T>> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo = null)
    {
        try
        {
            using var peticion = await CrearPeticionAsync(metodo, url, cuerpo);
            using var response = await _httpClient.SendAsync(peticion);
            return await LeerRespuestaAsync<T>(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en {metodo} {url}: {ex.Message}");
            return ApiResponse<T>.ErrorResponse("No se pudo conectar con el servidor. Compruebe la conexión e intente de nuevo.");
        }
    }

    /// <summary>
    /// Lee un ApiResponse; si el cuerpo es un ProblemDetails de validación ([ApiController])
    /// junta sus errores, y si no hay cuerpo construye el mensaje a partir del código HTTP.
    /// </summary>
    private static async Task<ApiResponse<T>> LeerRespuestaAsync<T>(HttpResponseMessage response)
    {
        var texto = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(texto))
        {
            try
            {
                using var documento = JsonDocument.Parse(texto);
                var raiz = documento.RootElement;
                if (raiz.ValueKind == JsonValueKind.Object)
                {
                    if (raiz.TryGetProperty("success", out _))
                    {
                        var api = raiz.Deserialize<ApiResponse<T>>(OpcionesJson);
                        if (api != null)
                        {
                            if (!api.Success && string.IsNullOrWhiteSpace(api.Message))
                            {
                                api.Message = MensajeSegunEstado(response.StatusCode);
                            }

                            return api;
                        }
                    }

                    if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
                    {
                        var lista = errores.EnumerateObject()
                            .SelectMany(campo => campo.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty))
                            .Where(e => e.Length > 0)
                            .ToList();
                        return ApiResponse<T>.ErrorResponse(
                            lista.Count > 0 ? string.Join(" ", lista) : MensajeSegunEstado(response.StatusCode), lista);
                    }
                }
            }
            catch (JsonException)
            {
                // Cuerpo que no es JSON (p. ej. una página de error): se usa el código HTTP.
            }
        }

        return ApiResponse<T>.ErrorResponse(MensajeSegunEstado(response.StatusCode));
    }

    private static string MensajeSegunEstado(HttpStatusCode estado) => (int)estado switch
    {
        400 => "La solicitud no es válida.",
        401 => "Su sesión no es válida. Inicie sesión de nuevo.",
        403 => "No tiene permiso para realizar esta acción.",
        404 => "Esta función todavía no está disponible en el servidor.",
        405 => "El servidor ya no admite esta acción.",
        409 => "La acción no es posible en el estado actual del registro.",
        423 => "La cuenta está bloqueada temporalmente.",
        _ => $"El servidor respondió con un error ({(int)estado})."
    };

    /// <summary>Añade a la ruta los filtros con valor, con fechas en formato ISO (yyyy-MM-dd).</summary>
    private static string ConFiltros(string ruta, params (string Clave, object? Valor)[] filtros)
    {
        var partes = new List<string>();
        foreach (var (clave, valor) in filtros)
        {
            var texto = valor switch
            {
                null => null,
                string s when string.IsNullOrWhiteSpace(s) => null,
                string s => s.Trim(),
                DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => valor.ToString()
            };

            if (texto != null)
            {
                partes.Add($"{clave}={Uri.EscapeDataString(texto)}");
            }
        }

        if (partes.Count == 0)
        {
            return ruta;
        }

        return ruta + (ruta.Contains('?') ? "&" : "?") + string.Join("&", partes);
    }

    /// <summary>
    /// Descarga un archivo del API (PDF, Excel) y lo entrega al navegador con wwwroot/js/descargas.js.
    /// El archivo pasa por un DotNetStreamReference: no se convierte a base64 ni se carga dos veces.
    /// </summary>
    public async Task<ApiResponse<bool>> DescargarArchivoAsync(string url, string nombreArchivo)
    {
        try
        {
            using var peticion = await CrearPeticionAsync(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(peticion);

            if (!response.IsSuccessStatusCode)
            {
                var error = await LeerRespuestaAsync<bool>(response);
                return ApiResponse<bool>.ErrorResponse(error.Message, error.Errors);
            }

            var tipo = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            await using var contenido = await response.Content.ReadAsStreamAsync();
            using var referencia = new DotNetStreamReference(contenido);
            await _jsRuntime.InvokeVoidAsync("ecarDescargas.guardar", nombreArchivo, tipo, referencia);

            return ApiResponse<bool>.SuccessResponse(true, "Archivo descargado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error descargando {url}: {ex.Message}");
            return ApiResponse<bool>.ErrorResponse("No se pudo descargar el archivo. Compruebe la conexión e intente de nuevo.");
        }
    }

    // --- Auditoría (endpoints 1, 2, 3 y 21) ---

    public Task<ApiResponse<PagedResultDto<AuditoriaDto>>> BuscarAuditoriaAsync(AuditoriaFiltroDto filtro,
        int page = 1, int pageSize = 20) =>
        EnviarAsync<PagedResultDto<AuditoriaDto>>(HttpMethod.Get, ConFiltros("api/auditoria",
            ("page", page), ("pageSize", pageSize), ("search", filtro.Search), ("tabla", filtro.Tabla),
            ("registroId", filtro.RegistroId), ("idUsuario", filtro.IdUsuario), ("accion", filtro.Accion),
            ("desde", filtro.Desde), ("hasta", filtro.Hasta)));

    /// <summary>Todas las filas de auditoría de un registro, en orden (historial de cambios).</summary>
    public Task<ApiResponse<List<AuditoriaDto>>> GetHistorialRegistroAsync(string tabla, long registroId) =>
        EnviarAsync<List<AuditoriaDto>>(HttpMethod.Get,
            $"api/auditoria/registro/{Uri.EscapeDataString(tabla)}/{registroId}");

    public Task<ApiResponse<VerificacionAuditoriaDto>> VerificarAuditoriaAsync(DateTime? desde = null, DateTime? hasta = null) =>
        EnviarAsync<VerificacionAuditoriaDto>(HttpMethod.Get,
            ConFiltros("api/auditoria/verificar", ("desde", desde), ("hasta", hasta)));

    public Task<ApiResponse<bool>> DescargarAuditoriaAsync(AuditoriaFiltroDto filtro, string formato) =>
        DescargarArchivoAsync(ConFiltros("api/auditoria/exportar",
                ("formato", formato), ("search", filtro.Search), ("tabla", filtro.Tabla),
                ("registroId", filtro.RegistroId), ("idUsuario", filtro.IdUsuario), ("accion", filtro.Accion),
                ("desde", filtro.Desde), ("hasta", filtro.Hasta)),
            $"ECAR_auditoria_{DateTime.Now:yyyyMMdd_HHmm}.{formato}");

    // --- Hallazgos (endpoints 4, 5, 8 y 9; el alta y la edición usan Create/UpdateHallazgoAsync) ---

    public Task<ApiResponse<PagedResultDto<HallazgoDto>>> BuscarHallazgosAsync(HallazgoFiltroDto filtro,
        int page = 1, int pageSize = 10) =>
        EnviarAsync<PagedResultDto<HallazgoDto>>(HttpMethod.Get, ConFiltros("api/hallazgos",
            ("page", page), ("pageSize", pageSize), ("search", filtro.Search), ("estado", filtro.Estado),
            ("criticidad", filtro.Criticidad), ("idEquipo", filtro.IdEquipo), ("idInspeccion", filtro.IdInspeccion),
            ("desde", filtro.Desde), ("hasta", filtro.Hasta)));

    public Task<ApiResponse<HallazgoDetalleDto>> GetHallazgoDetalleAsync(long id) =>
        EnviarAsync<HallazgoDetalleDto>(HttpMethod.Get, $"api/hallazgos/{id}");

    public Task<ApiResponse<HallazgoDto>> CambiarEstadoHallazgoAsync(long id, CambiarEstadoHallazgoDto cambio) =>
        EnviarAsync<HallazgoDto>(HttpMethod.Post, $"api/hallazgos/{id}/estado", cambio);

    public Task<ApiResponse<HallazgoDto>> AnularHallazgoAsync(long id, string motivo) =>
        EnviarAsync<HallazgoDto>(HttpMethod.Post, $"api/hallazgos/{id}/anular", new MotivoDto { Motivo = motivo });

    // --- Inspecciones y evidencias (endpoints 10, 11, 13 y 14) ---

    /// <summary>Sustituye a DeleteInspeccionAsync: la inspección en curso se anula con motivo y se conserva.</summary>
    public Task<ApiResponse<InspeccionDto>> AnularInspeccionAsync(long id, string motivo) =>
        EnviarAsync<InspeccionDto>(HttpMethod.Post, $"api/inspecciones/{id}/anular", new MotivoDto { Motivo = motivo });

    /// <summary>Sustituye a DeleteEvidenciaAsync: la foto se marca como retirada y el archivo se conserva.</summary>
    public Task<ApiResponse<EvidenciaDto>> RetirarEvidenciaAsync(long id, string motivo) =>
        EnviarAsync<EvidenciaDto>(HttpMethod.Post, $"api/evidencias/{id}/retirar", new MotivoDto { Motivo = motivo });

    public Task<ApiResponse<IntegridadInspeccionDto>> VerificarIntegridadInspeccionAsync(long id) =>
        EnviarAsync<IntegridadInspeccionDto>(HttpMethod.Get, $"api/inspecciones/{id}/integridad");

    /// <summary>Copia completa y legible de una inspección firmada (Parte 11 §11.10(b)).</summary>
    public Task<ApiResponse<bool>> DescargarRegistroInspeccionAsync(long id) =>
        DescargarArchivoAsync($"api/inspecciones/{id}/registro.pdf", $"ECAR_inspeccion_{id}.pdf");

    // --- Reportes (endpoints 15 a 20) ---

    /// <summary>
    /// Vista previa de un reporte (formato=json). T es PagedResultDto de la fila del reporte, o
    /// InspeccionesPorFechasDto para el de inspecciones por fechas.
    /// </summary>
    public Task<ApiResponse<T>> GetReporteAsync<T>(string tipo, ReporteFiltroDto filtro, int page = 1, int pageSize = 50) =>
        EnviarAsync<T>(HttpMethod.Get, UrlReporte(tipo, filtro, ReporteFormatos.Json, page, pageSize));

    /// <summary>Descarga un reporte en PDF o Excel (ReporteFormatos.Pdf / ReporteFormatos.Excel).</summary>
    public Task<ApiResponse<bool>> DescargarReporteAsync(string tipo, ReporteFiltroDto filtro, string formato) =>
        DescargarArchivoAsync(UrlReporte(tipo, filtro, formato, null, null),
            $"ECAR_{tipo}_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}.{formato}");

    private static string UrlReporte(string tipo, ReporteFiltroDto filtro, string formato, int? page, int? pageSize) =>
        ConFiltros($"api/reportes/{tipo}",
            ("formato", formato), ("page", page), ("pageSize", pageSize),
            ("desde", filtro.Desde), ("hasta", filtro.Hasta), ("planta", filtro.Planta), ("area", filtro.Area),
            ("idEquipo", filtro.IdEquipo), ("idUsuario", filtro.IdUsuario), ("estado", filtro.Estado),
            ("criticidad", filtro.Criticidad));

    // --- Cuentas (endpoints 23 a 26; el login sigue en AuthService) ---

    public Task<ApiResponse<bool>> CambiarPasswordAsync(CambiarPasswordDto cambio) =>
        EnviarAsync<bool>(HttpMethod.Post, "api/auth/cambiar-password", cambio);

    /// <summary>Política de contraseñas y minutos de inactividad. Es anónimo.</summary>
    public Task<ApiResponse<PoliticaSeguridadDto>> GetPoliticaSeguridadAsync() =>
        EnviarAsync<PoliticaSeguridadDto>(HttpMethod.Get, "api/auth/politica");

    public Task<ApiResponse<PasswordTemporalDto>> RestablecerPasswordAsync(long idUsuario, string motivo) =>
        EnviarAsync<PasswordTemporalDto>(HttpMethod.Post, $"api/usuarios/{idUsuario}/restablecer-password",
            new MotivoDto { Motivo = motivo });

    public Task<ApiResponse<bool>> DesbloquearUsuarioAsync(long idUsuario, string motivo) =>
        EnviarAsync<bool>(HttpMethod.Post, $"api/usuarios/{idUsuario}/desbloquear", new MotivoDto { Motivo = motivo });
}
