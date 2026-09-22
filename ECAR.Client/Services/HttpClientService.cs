using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Components.Forms;
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
        string? search = null, string? criticidad = null)
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
        string? search = null)
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
    /// </summary>
    public async Task<ApiResponse<List<RespuestaInspeccionDto>>?> GuardarRespuestasAsync(long idInspeccion,
        GuardarRespuestasDto guardarDto)
    {
        try
        {
            await AddAuthorizationHeaderAsync();
            var response = await _httpClient.PutAsJsonAsync($"api/inspecciones/{idInspeccion}/respuestas", guardarDto);
            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<List<RespuestaInspeccionDto>>>();
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
}
