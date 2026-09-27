using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;

namespace ECAR.Client.Services;

public class MockDataService
{
    // ===================== ESTADO EN MEMORIA =====================
    // Mock temporal para la pantalla de Respuestas de inspección (Fase 3), aún sin backend.
    // Ubicaciones y Preguntas de checklist ya usan el API real vía HttpClientService.
    private static List<PreguntaChecklistDto> _preguntas = new()
    {
        new PreguntaChecklistDto { IdPregunta = 1, IdChecklist = 1, Pregunta = "¿El equipo enciende correctamente?", TipoRespuesta = "SiNo", Obligatoria = true },
        new PreguntaChecklistDto { IdPregunta = 2, IdChecklist = 1, Pregunta = "¿Presenta fugas visibles?", TipoRespuesta = "SiNo", Obligatoria = true },
        new PreguntaChecklistDto { IdPregunta = 3, IdChecklist = 1, Pregunta = "Observaciones generales", TipoRespuesta = "Texto", Obligatoria = false },
    };

    private static List<RespuestaInspeccionDto> _respuestas = new()
    {
        new RespuestaInspeccionDto { IdRespuesta = 1, IdInspeccion = 1, IdPregunta = 1, Respuesta = "Sí", Observacion = "Sin novedad" },
        new RespuestaInspeccionDto { IdRespuesta = 2, IdInspeccion = 1, IdPregunta = 2, Respuesta = "No", Observacion = "" },
    };
    private static long _nextRespuestaId = 3;

    private async Task<T> SimulateDelay<T>(T value)
    {
        await Task.Delay(300); // simula latencia de red
        return value;
    }

    private static PagedResultDto<T> Paginate<T>(List<T> source, int page, int pageSize)
    {
        var total = source.Count;
        var items = source.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResultDto<T>
        {
            Data = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ===================== PREGUNTAS CHECKLIST =====================
    // El CRUD de preguntas ya usa el API real (PreguntasChecklistController). Solo queda el lookup
    // que consume RespuestaInspeccionModal mientras Respuestas de inspección sigue en mock (Fase 3).
    public async Task<ApiResponse<List<PreguntaChecklistDto>>?> GetPreguntasChecklistLookupAsync()
    {
        var result = ApiResponse<List<PreguntaChecklistDto>>.SuccessResponse(_preguntas.ToList());
        return await SimulateDelay(result);
    }

    // ===================== RESPUESTAS INSPECCION =====================
    public async Task<ApiResponse<PagedResultDto<RespuestaInspeccionDto>>?> GetRespuestasInspeccionAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        var query = _respuestas.AsEnumerable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(r => (r.Respuesta ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));

        var result = ApiResponse<PagedResultDto<RespuestaInspeccionDto>>.SuccessResponse(Paginate(query.ToList(), page, pageSize));
        return await SimulateDelay(result);
    }

    public async Task<ApiResponse<RespuestaInspeccionDto>?> CreateRespuestaInspeccionAsync(CreateRespuestaInspeccionDto dto)
    {
        var nueva = new RespuestaInspeccionDto { IdRespuesta = _nextRespuestaId++, IdInspeccion = dto.IdInspeccion, IdPregunta = dto.IdPregunta, Respuesta = dto.Respuesta, Observacion = dto.Observacion };
        _respuestas.Add(nueva);
        return await SimulateDelay(ApiResponse<RespuestaInspeccionDto>.SuccessResponse(nueva, "Respuesta creada exitosamente"));
    }

    public async Task<ApiResponse<RespuestaInspeccionDto>?> UpdateRespuestaInspeccionAsync(long id, UpdateRespuestaInspeccionDto dto)
    {
        var existente = _respuestas.FirstOrDefault(r => r.IdRespuesta == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<RespuestaInspeccionDto>.ErrorResponse("Respuesta no encontrada"));

        existente.Respuesta = dto.Respuesta;
        existente.Observacion = dto.Observacion;
        return await SimulateDelay(ApiResponse<RespuestaInspeccionDto>.SuccessResponse(existente, "Respuesta actualizada exitosamente"));
    }

    public async Task<ApiResponse<bool>?> DeleteRespuestaInspeccionAsync(long id)
    {
        var existente = _respuestas.FirstOrDefault(r => r.IdRespuesta == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<bool>.ErrorResponse("Respuesta no encontrada"));

        _respuestas.Remove(existente);
        return await SimulateDelay(ApiResponse<bool>.SuccessResponse(true, "Respuesta eliminada exitosamente"));
    }
}