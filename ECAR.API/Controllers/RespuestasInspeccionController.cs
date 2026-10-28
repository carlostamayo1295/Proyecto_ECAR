using ECAR.API.Services;
using ECAR.Shared;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RolesSistema.Todos)]
public class RespuestasInspeccionController : ControllerBase
{
    private readonly IInspeccionService _inspeccionService;
    private readonly ICurrentUser _currentUser;

    public RespuestasInspeccionController(IInspeccionService inspeccionService, ICurrentUser currentUser)
    {
        _inspeccionService = inspeccionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<RespuestaInspeccionDto>>>> GetRespuestas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] long? idInspeccion = null)
    {
        // Máximo 100 por página, como en el resto de listados: sin límite, pageSize=100000 devolvía la tabla entera.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var pagedResult = await _inspeccionService.ObtenerRespuestasPaginadasAsync(
            page, pageSize, search, idInspeccion, _currentUser.IdUsuario, AccesoInspeccion.LeeTodas(_currentUser));

        return Ok(ApiResponse<PagedResultDto<RespuestaInspeccionDto>>.SuccessResponse(pagedResult));
    }
}