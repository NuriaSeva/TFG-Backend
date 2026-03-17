using FindMind.Data;
using FindMind.Models.Enitdades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuentasBancariasController : ControllerBase
{
    private readonly FindMindDbContext _context;

    public CuentasBancariasController(FindMindDbContext context)
    {
        _context = context;
    }

    [HttpGet("usuario/{usuarioId}")]
    public async Task<ActionResult<CuentaBancaria>> GetCuentaPorUsuario(Guid usuarioId)
    {
        try
        {
            var cuenta = await _context.CuentasBancarias
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

            if (cuenta == null)
                return NotFound("El usuario no tiene cuenta bancaria.");

            return Ok(cuenta);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error obteniendo la cuenta: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<CuentaBancaria>> CrearCuenta(CuentaBancaria cuenta)
    {
        try
        {
            var existe = await _context.CuentasBancarias
                .AnyAsync(c => c.UsuarioId == cuenta.UsuarioId);

            if (existe)
                return BadRequest("El usuario ya tiene una cuenta bancaria.");

            cuenta.Id = Guid.NewGuid();
            cuenta.FechaCreacion = DateTime.UtcNow;

            _context.CuentasBancarias.Add(cuenta);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCuentaPorUsuario),
                new { usuarioId = cuenta.UsuarioId }, cuenta);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creando cuenta bancaria: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
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