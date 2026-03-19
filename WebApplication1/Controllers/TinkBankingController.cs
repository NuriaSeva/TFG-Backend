using FindMind.DTO;
using FindMind.DTO.Banking;
using FindMind.Interfaces;
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
    public async Task<IActionResult> Callback([FromQuery] string localUserId )
    {
        var queryParams = Request.Query
            .ToDictionary(k => k.Key, v => v.Value.ToString());

        var result = await _service.HandleAccountCheckCallbackAsync(localUserId, queryParams);
        return Ok(result);
    }

    [HttpGet("account-check/last-result")]
    public async Task<IActionResult> GetLastAccountCheckResult([FromQuery] string localUserId )
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

    [HttpGet("account-check/report-from-last")]
    public async Task<IActionResult> GetReportFromLastResult([FromQuery] string localUserId)
    {
        var lastResult = await _service.GetLastAccountCheckResultAsync(localUserId);

        if (lastResult == null)
            return NotFound(new { message = "No hay resultado de Account Check para ese usuario." });

        if (string.IsNullOrWhiteSpace(lastResult.AccountVerificationReportId))
            return BadRequest(new { message = "El último callback no contiene account_verification_report_id." });

        var reportJson = await _service.GetAccountVerificationReportRawAsync(lastResult.AccountVerificationReportId);
        return Content(reportJson, "application/json");
    }

    [HttpGet("transactions/raw")]
    public async Task<IActionResult> GetTransactions([FromQuery] string localUserId )
    {
        var result = await _service.GetTransactionsRawAsync(localUserId);
        return Content(result, "application/json");
    }

    [HttpGet("accounts/raw")]
    public async Task<IActionResult> GetAccounts([FromQuery] string localUserId = "usuario-demo")
    {
        var result = await _service.GetAccountsRawAsync(localUserId);
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
}