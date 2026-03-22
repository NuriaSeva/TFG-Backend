using FinMind.DTO;
using FinMind.DTO.Banking;
using FinMind.Interfaces;
using FinMind.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/banking/tink")]
public class BankingController : ControllerBase
{
    private readonly ITinkBankingService _service;

    public BankingController(ITinkBankingService service)
    {
        _service = service;
    }

    [HttpGet("login-url")]
    public async Task<IActionResult> GetLoginUrl([FromQuery] string localUserId)
    {
        var result = await _service.GetLoginUrlAsync(localUserId);
        return Ok(result);
    }

    [HttpGet("callback")]
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

            return Redirect("FinMind://callback?status=connected");
        }
        catch (Exception ex)
        {
            return Redirect("FinMind://callback?status=error&message=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("account-check/last-result")]
    public async Task<IActionResult> GetLastAccountCheckResult([FromQuery] string localUserId)
    {
        var result = await _service.GetLastAccountCheckResultAsync(localUserId);

        if (result == null)
            return NotFound(new { message = "No hay resultado de Account Check para ese usuario." });

        return Ok(result);
    }

    [HttpGet("account-check/report")]
    public async Task<IActionResult> GetAccountCheckReport([FromQuery] string reportId)
    {
        var result = await _service.GetAccountVerificationReportRawAsync(reportId);
        return Content(result, "application/json");
    }


    [HttpPost("account-check/guardar-cuenta")]
    public async Task<IActionResult> GuardarCuentaDesdeAccountCheck([FromBody] GuardarCuentaDesdeReporteRequestDto request)
    {
        var result = await _service.GuardarCuentaDesdeAccountCheckAsync(
            request.UsuarioId,
            request.ReportId);

        return Ok(result);
    }

    [HttpGet("transactions/login-url")]
    public async Task<IActionResult> GetTransactionsLoginUrl([FromQuery] string localUserId)
    {
        var result = await _service.GetTransactionsLoginUrlAsync(localUserId);
        return Ok(result);
    }

    [HttpGet("transactions/callback")]
    public async Task<IActionResult> TransactionsCallback([FromQuery] string localUserId, [FromQuery] string code)
    {
        try
        {
            var usuarioId = Guid.Parse(localUserId);

            await _service.GuardarTokensTransactionsAsync(usuarioId, code);

            return Redirect("findmind://callback?status=transactions-connected");
        }
        catch (Exception ex)
        {
            return Redirect(
                "findmind://callback?status=transactions-error&message=" +
                Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("transactions/{usuarioId:guid}")]
    public async Task<IActionResult> GetTransactions([FromQuery] Guid usuarioId, [FromQuery]  string? cuentaExternaId, [FromQuery] Guid idCuenta)
    {
        var raw = await _service.GetTransactionsRawAsync(usuarioId, cuentaExternaId, idCuenta);
        return Content(raw, "application/json");
    }
}