using ECAR.Infrastructure.Data;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
public class RespuestasInspeccionController : ControllerBase
{
    private readonly ECARDbContext _context;

    public RespuestasInspeccionController(ECARDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<RespuestaInspeccionDto>>>> GetRespuestas([FromQuery] PagedRequestDto request)
    {
        var query = _context.RespuestasInspeccion.AsNoTracking();

        var totalCount = await query.CountAsync();

        var respuestas = await query
            .OrderByDescending(r => r.IdRespuesta)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new RespuestaInspeccionDto
            {
                IdRespuesta = r.IdRespuesta,
                IdInspeccion = r.IdInspeccion,
                IdPregunta = r.IdPregunta,
                Respuesta = r.Respuesta,
                Observacion = r.Observacion
            })
            .ToListAsync();

        var pagedResult = new PagedResultDto<RespuestaInspeccionDto>
        {
            Data = respuestas,
            TotalCount = totalCount,
            Page = request.PageNumber,
            PageSize = request.PageSize
        };

        return Ok(ApiResponse<PagedResultDto<RespuestaInspeccionDto>>.SuccessResponse(pagedResult));
    }
}