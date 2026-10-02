using ECAR.API.Services;
using ECAR.Shared.DTOs;
using ECAR.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECAR.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Técnico,Auditor")]
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
        var usuarioId = _currentUser.IdUsuario;
        var esAdmin = _currentUser.IsInRole("Administrador");
        var esAuditor = _currentUser.IsInRole("Auditor");

        var pagedResult = await _inspeccionService.ObtenerRespuestasPaginadasAsync(
            page, pageSize, search, idInspeccion, usuarioId, esAdmin, esAuditor);

        return Ok(ApiResponse<PagedResultDto<RespuestaInspeccionDto>>.SuccessResponse(pagedResult));
    }
}