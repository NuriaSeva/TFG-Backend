using FinMind.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : BaseController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("resumen")]
    [Authorize]
    public async Task<IActionResult> ObtenerResumen([FromQuery] int? mes = null, [FromQuery] int? anio = null)
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerResumenMesActualAsync(usuarioId, mes, anio);
        return Ok(resultado);
    }

    [HttpGet("visualizaciones")]
    [Authorize]
    public async Task<IActionResult> ObtenerVisualizaciones([FromQuery] int? mes = null, [FromQuery] int? anio = null)
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerVisualizacionesAsync(usuarioId, mes, anio);
        return Ok(resultado);
    }

    [HttpGet("mapa-calor")]
    [Authorize]
    public async Task<IActionResult> ObtenerMapaCalor([FromQuery] int? mes = null, [FromQuery] int? anio = null)
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerMapaCalorMesActualAsync(usuarioId, mes, anio);
        return Ok(resultado);
    }
}