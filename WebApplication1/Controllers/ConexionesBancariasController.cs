using FindMind.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConexionesBancariasController : ControllerBase
{
    private readonly TrueLayerService _trueLayerService;

    public ConexionesBancariasController(TrueLayerService trueLayerService)
    {
        _trueLayerService = trueLayerService;
    }

    [HttpPost("token")]
    public async Task<IActionResult> ObtenerTokenDesdeCode([FromBody] ObtenerTokenRequest request, CancellationToken cancellationToken)
    {
        var token = await _trueLayerService.IntercambiarCodePorTokenAsync(request.Code, cancellationToken);
        return Ok(token);
    }

    [HttpGet("cuentas")]
    public async Task<IActionResult> ObtenerCuentas([FromQuery] string accessToken, CancellationToken cancellationToken)
    {
        var cuentas = await _trueLayerService.ObtenerCuentasRawAsync(accessToken, cancellationToken);
        return Content(cuentas, "application/json");
    }

    [HttpGet("transacciones")]
    public async Task<IActionResult> ObtenerTransacciones(
        [FromQuery] string accountId,
        [FromQuery] string accessToken,
        CancellationToken cancellationToken)
    {
        var transacciones = await _trueLayerService.ObtenerTransaccionesRawAsync(accountId, accessToken, cancellationToken);
        return Content(transacciones, "application/json");
    }
}

public class ObtenerTokenRequest
{
    public string Code { get; set; } = null!;
}