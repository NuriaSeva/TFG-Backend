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
    public async Task<IActionResult> GetLoginUrl([FromQuery] string localUserId = "usuario-demo")
    {
        var result = await _service.GetLoginUrlAsync(localUserId);
        return Ok(result);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string localUserId = "usuario-demo")
    {
        var result = await _service.ExchangeCodeAsync(localUserId, code);
        return Ok(result);
    }

    [HttpGet("transactions/raw")]
    public async Task<IActionResult> GetTransactions([FromQuery] string localUserId = "usuario-demo")
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
}