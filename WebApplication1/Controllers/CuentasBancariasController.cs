using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuentasBancariasController : BaseController
{
    private readonly ICuentasBancariasService _cuentasBancariasService;

    public CuentasBancariasController(ICuentasBancariasService cuentasBancariasService)
    {
        _cuentasBancariasService = cuentasBancariasService;
    }

    [HttpGet("usuario")]
    [Authorize]
    public async Task<ActionResult<CuentaSeleccionadaResponseDto>> GetCuentaPorUsuario()
    {
        var usuarioId = ObtenerUsuarioId();
        var cuenta = await _cuentasBancariasService.ObtenerCuentaPorUsuarioAsync(usuarioId);

        if (cuenta == null)
            return NotFound();

        return Ok(cuenta);
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> ActualizarCuenta(Guid id, CuentaBancaria cuenta)
    {
        await _cuentasBancariasService.ActualizarCuentaAsync(id, cuenta);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> EliminarCuenta(Guid id)
    {
        await _cuentasBancariasService.EliminarCuentaAsync(id);
        return NoContent();
    }
}
