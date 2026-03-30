using FinMind.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("resumen/{usuarioId:guid}")]
    public async Task<IActionResult> ObtenerResumen(Guid usuarioId)
    {
        var resultado = await _dashboardService.ObtenerResumenMesActualAsync(usuarioId);
        return Ok(resultado);
    }
}