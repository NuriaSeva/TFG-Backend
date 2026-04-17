using FinMind.Controllers;
using FinMind.DTO;
using FinMind.DTO.Banking;
using FinMind.Interfaces;
using FinMind.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/banking/tink")]
public class BankingController : BaseController
{
    private readonly ITinkBankingService _service;

    public BankingController(ITinkBankingService service)
    {
        _service = service;
    }

    [HttpGet("login-url")]
    [Authorize]
    public async Task<IActionResult> GetLoginUrl()
    {
        var usuarioId = ObtenerUsuarioId().ToString(); ;
        var result = await _service.GetLoginUrlAsync(usuarioId);
        return Ok(result);
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
    [FromQuery] string localUserId)
    {
        var userId = localUserId;
        try
        {
            var queryParams = Request.Query
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToString()
                );

            await _service.ProcesarCallbackYGuardarCuentaAsync(
                localUserId,
                queryParams);

            return Redirect("finmind://callback?status=connected");
        }
        catch (Exception ex)
        {
            return Redirect("finmind://callback?status=error&message=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("account-check/last-result")]
    [Authorize]
    public async Task<IActionResult> GetLastAccountCheckResult()
    {
        var usuarioId = ObtenerUsuarioId().ToString(); ;
        var result = await _service.GetLastAccountCheckResultAsync(usuarioId);

        if (result == null)
            return NotFound(new { message = "No hay resultado de Account Check para ese usuario." });

        return Ok(result);
    }

    [HttpGet("account-check/report")]
    [Authorize]
    public async Task<IActionResult> GetAccountCheckReport([FromQuery] string reportId)
    {
        var result = await _service.GetAccountVerificationReportRawAsync(reportId);
        return Content(result, "application/json");
    }


    [HttpPost("account-check/guardar-cuenta")]
    [Authorize]
    public async Task<IActionResult> GuardarCuentaDesdeAccountCheck([FromBody] GuardarCuentaDesdeReporteRequestDto request)
    {
        var result = await _service.GuardarCuentaDesdeAccountCheckAsync(
            request.UsuarioId,
            request.ReportId);

        return Ok(result);
    }


    [HttpGet("transactions/login-url")]
    [Authorize]
    public async Task<IActionResult> GetTransactionsLoginUrl() {
        var usuarioId = ObtenerUsuarioId().ToString();
        var result = await _service.GetTransactionsLoginUrlAsync(usuarioId);
        return Ok(result);
    }

    [HttpGet("transactions/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> TransactionsCallback([FromQuery] string localUserId, [FromQuery] string code)
    {
        try
        {
            var usuarioId = Guid.Parse(localUserId);

            await _service.GuardarTokensTransactionsAsync(usuarioId, code);

            return Redirect("finmind://callback?status=transactions-connected");
        }
        catch (Exception ex)
        {
            return Redirect(
                "finmind://callback?status=transactions-error&message=" +
                Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("transactions")]
    [Authorize]
    public async Task<IActionResult> GetTransactions([FromQuery]  string? cuentaExternaId, [FromQuery] Guid idCuenta)
    {
        var usuarioId = ObtenerUsuarioId();
        var raw = await _service.GetTransactionsRawAsync(usuarioId, cuentaExternaId, idCuenta);
        return Content(raw, "application/json");
    }
    [HttpPost("desvincular")]
    [Authorize]
    public async Task<IActionResult> Desvincular()
    {
        var usuarioId = ObtenerUsuarioId();
        await _service.DesvincularCuentaAsync(usuarioId);
        return Ok(new { mensaje = "Cuenta desvinculada correctamente." });
    }
}