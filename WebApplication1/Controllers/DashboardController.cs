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
    public async Task<IActionResult> ObtenerResumen()
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerResumenMesActualAsync(usuarioId);
        return Ok(resultado);
    }

    [HttpGet("visualizaciones")]
    [Authorize]
    public async Task<IActionResult> ObtenerVisualizaciones()
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerVisualizacionesAsync(usuarioId);
        return Ok(resultado);
    }

    [HttpGet("mapa-calor")]
    [Authorize]
    public async Task<IActionResult> ObtenerMapaCalor()
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerMapaCalorMesActualAsync(usuarioId);
        return Ok(resultado);
    }
}