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
}