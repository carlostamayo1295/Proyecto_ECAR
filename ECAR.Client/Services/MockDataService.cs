using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;

namespace ECAR.Client.Services;

public class MockDataService
{
    // ===================== ESTADO EN MEMORIA =====================
    private static List<UbicacionDto> _ubicaciones = new()
    {
        new UbicacionDto { IdUbicacion = 1, Planta = "Planta Norte", Area = "Producción", Descripcion = "Área de ensamblaje principal" },
        new UbicacionDto { IdUbicacion = 2, Planta = "Planta Norte", Area = "Almacén", Descripcion = "Bodega de insumos" },
        new UbicacionDto { IdUbicacion = 3, Planta = "Planta Sur", Area = "Calidad", Descripcion = "Laboratorio de control de calidad" },
    };
    private static long _nextUbicacionId = 4;

    private static List<PreguntaChecklistDto> _preguntas = new()
    {
        new PreguntaChecklistDto { IdPregunta = 1, IdChecklist = 1, Pregunta = "¿El equipo enciende correctamente?", TipoRespuesta = "SiNo", Obligatoria = true },
        new PreguntaChecklistDto { IdPregunta = 2, IdChecklist = 1, Pregunta = "¿Presenta fugas visibles?", TipoRespuesta = "SiNo", Obligatoria = true },
        new PreguntaChecklistDto { IdPregunta = 3, IdChecklist = 1, Pregunta = "Observaciones generales", TipoRespuesta = "Texto", Obligatoria = false },
    };
    private static long _nextPreguntaId = 4;

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

    // ===================== UBICACIONES =====================
    public async Task<ApiResponse<PagedResultDto<UbicacionDto>>?> GetUbicacionesAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        var query = _ubicaciones.AsEnumerable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Planta.Contains(search, StringComparison.OrdinalIgnoreCase)
                                   || u.Area.Contains(search, StringComparison.OrdinalIgnoreCase));

        var result = ApiResponse<PagedResultDto<UbicacionDto>>.SuccessResponse(Paginate(query.ToList(), page, pageSize));
        return await SimulateDelay(result);
    }

    public async Task<ApiResponse<UbicacionDto>?> CreateUbicacionAsync(CreateUbicacionDto dto)
    {
        var nueva = new UbicacionDto { IdUbicacion = _nextUbicacionId++, Planta = dto.Planta, Area = dto.Area, Descripcion = dto.Descripcion };
        _ubicaciones.Add(nueva);
        return await SimulateDelay(ApiResponse<UbicacionDto>.SuccessResponse(nueva, "Ubicación creada exitosamente"));
    }

    public async Task<ApiResponse<UbicacionDto>?> UpdateUbicacionAsync(long id, UpdateUbicacionDto dto)
    {
        var existente = _ubicaciones.FirstOrDefault(u => u.IdUbicacion == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<UbicacionDto>.ErrorResponse("Ubicación no encontrada"));

        existente.Planta = dto.Planta;
        existente.Area = dto.Area;
        existente.Descripcion = dto.Descripcion;
        return await SimulateDelay(ApiResponse<UbicacionDto>.SuccessResponse(existente, "Ubicación actualizada exitosamente"));
    }

    public async Task<ApiResponse<bool>?> DeleteUbicacionAsync(long id)
    {
        var existente = _ubicaciones.FirstOrDefault(u => u.IdUbicacion == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<bool>.ErrorResponse("Ubicación no encontrada"));

        _ubicaciones.Remove(existente);
        return await SimulateDelay(ApiResponse<bool>.SuccessResponse(true, "Ubicación eliminada exitosamente"));
    }

    // ===================== PREGUNTAS CHECKLIST =====================
    public async Task<ApiResponse<PagedResultDto<PreguntaChecklistDto>>?> GetPreguntasChecklistAsync(int page = 1, int pageSize = 10, string? search = null)
    {
        var query = _preguntas.AsEnumerable();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Pregunta.Contains(search, StringComparison.OrdinalIgnoreCase));

        var result = ApiResponse<PagedResultDto<PreguntaChecklistDto>>.SuccessResponse(Paginate(query.ToList(), page, pageSize));
        return await SimulateDelay(result);
    }

    public async Task<ApiResponse<PreguntaChecklistDto>?> CreatePreguntaChecklistAsync(CreatePreguntaChecklistDto dto)
    {
        var nueva = new PreguntaChecklistDto { IdPregunta = _nextPreguntaId++, IdChecklist = dto.IdChecklist, Pregunta = dto.Pregunta, TipoRespuesta = dto.TipoRespuesta, Obligatoria = dto.Obligatoria };
        _preguntas.Add(nueva);
        return await SimulateDelay(ApiResponse<PreguntaChecklistDto>.SuccessResponse(nueva, "Pregunta creada exitosamente"));
    }

    public async Task<ApiResponse<PreguntaChecklistDto>?> UpdatePreguntaChecklistAsync(long id, UpdatePreguntaChecklistDto dto)
    {
        var existente = _preguntas.FirstOrDefault(p => p.IdPregunta == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<PreguntaChecklistDto>.ErrorResponse("Pregunta no encontrada"));

        existente.IdChecklist = dto.IdChecklist;
        existente.Pregunta = dto.Pregunta;
        existente.TipoRespuesta = dto.TipoRespuesta;
        existente.Obligatoria = dto.Obligatoria;
        return await SimulateDelay(ApiResponse<PreguntaChecklistDto>.SuccessResponse(existente, "Pregunta actualizada exitosamente"));
    }

    public async Task<ApiResponse<bool>?> DeletePreguntaChecklistAsync(long id)
    {
        var existente = _preguntas.FirstOrDefault(p => p.IdPregunta == id);
        if (existente == null)
            return await SimulateDelay(ApiResponse<bool>.ErrorResponse("Pregunta no encontrada"));

        _preguntas.Remove(existente);
        return await SimulateDelay(ApiResponse<bool>.SuccessResponse(true, "Pregunta eliminada exitosamente"));
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