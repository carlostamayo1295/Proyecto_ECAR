using ECAR.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using ECAR.API.Services;
namespace ECAR.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportesController : ControllerBase
    {
        private readonly IReporteService _reporteService;

        public ReportesController(IReporteService reporteService)
        {
            _reporteService = reporteService;
        }

        [HttpGet("historial-equipo")]
        public async Task<IActionResult> GetHistorialEquipo(
            [FromQuery] long? idEquipo,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetHistorialEquipoAsync(idEquipo, desde, hasta);
            return Ok(resultado);
        }

        [HttpGet("historial-area")]
        public async Task<IActionResult> GetHistorialArea(
            [FromQuery] string? planta,
            [FromQuery] string? area,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetHistorialAreaAsync(planta, area, desde, hasta);
            return Ok(resultado);
        }

        [HttpGet("historial-tecnico")]
        public async Task<IActionResult> GetHistorialTecnico(
            [FromQuery] long? idUsuario,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetHistorialTecnicoAsync(idUsuario, desde, hasta);
            return Ok(resultado);
        }

        [HttpGet("equipos-novedades")]
        public async Task<IActionResult> GetEquiposNovedades(
            [FromQuery] string? criticidad,
            [FromQuery] string? estado,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetEquiposNovedadesAsync(criticidad, estado, desde, hasta);
            return Ok(resultado);
        }

        [HttpGet("cumplimiento")]
        public async Task<IActionResult> GetCumplimiento(
            [FromQuery] string? planta,
            [FromQuery] string? area,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetCumplimientoAsync(planta, area, desde, hasta);
            return Ok(resultado);
        }

        [HttpGet("inspecciones-fechas")]
        public async Task<IActionResult> GetInspeccionesFechas(
            [FromQuery] string? planta,
            [FromQuery] string? area,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var resultado = await _reporteService.GetInspeccionesFechasAsync(planta, area, desde, hasta);
            return Ok(resultado);
        }
    }
}