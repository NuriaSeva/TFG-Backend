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
        Console.WriteLine("ENTRA EN /api/dashboard/resumen");

        var claim1 = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var claim2 = User.FindFirst("sub")?.Value;

        Console.WriteLine($"NameIdentifier: {claim1}");
        Console.WriteLine($"sub: {claim2}");
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _dashboardService.ObtenerResumenMesActualAsync(usuarioId);
        return Ok(resultado);
    }
}