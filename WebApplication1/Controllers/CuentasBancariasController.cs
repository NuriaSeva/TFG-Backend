using FinMind.Data;
using FinMind.DTO;
using FinMind.Models.Enitdades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuentasBancariasController : BaseController
{
    private readonly FinMindDbContext _context;

    public CuentasBancariasController(FinMindDbContext context)
    {
        _context = context;
    }

    [HttpGet("usuario")]
    [Authorize]
    public async Task<ActionResult<CuentaBancaria>> GetCuentaPorUsuario()
    {
        var usuarioId = ObtenerUsuarioId();
        var cuenta = await _context.CuentasBancarias
        .Where(c => c.UsuarioId == usuarioId && c.Activa)
        .OrderByDescending(c => c.FechaUltimaSincronizacion)
        .Select(c => new CuentaSeleccionadaResponseDto
        {
            Id = c.Id,
            IdCuentaExterna = c.IdCuentaExterna,
            Nombre = c.Nombre,
            Banco = c.Banco,
            Iban = c.Iban,
            Moneda = c.Moneda,
            Tipo = c.Tipo,
            FechaUltimaSincronizacion = c.FechaUltimaSincronizacion,
            SaldoActual = c.SaldoActual
        })
        .FirstOrDefaultAsync();

        if (cuenta == null)
            return NotFound();

        return Ok(cuenta);
    }



    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> ActualizarCuenta(Guid id, CuentaBancaria cuenta)
    {
        try
        {
            if (id != cuenta.Id)
                return BadRequest();

            var cuentaDb = await _context.CuentasBancarias.FindAsync(id);

            if (cuentaDb == null)
                return NotFound();

            cuentaDb.Nombre = cuenta.Nombre;
            cuentaDb.Iban = cuenta.Iban;
            cuentaDb.Banco = cuenta.Banco;
            cuentaDb.BIC = cuenta.BIC;
            cuentaDb.Moneda = cuenta.Moneda;
            cuentaDb.Tipo = cuenta.Tipo;
            cuentaDb.Activa = cuenta.Activa;

            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error actualizando cuenta: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> EliminarCuenta(Guid id)
    {
        try
        {
            var cuenta = await _context.CuentasBancarias.FindAsync(id);

            if (cuenta == null)
                return NotFound();

            _context.CuentasBancarias.Remove(cuenta);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error eliminando cuenta: {ex.Message}");
        }
    }
}