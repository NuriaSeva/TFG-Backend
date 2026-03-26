using FinMind.Common.Exceptions;
using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using FinMind.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransaccionesController : ControllerBase
{
    private readonly FinMindDbContext _context;

    private readonly ITransaccionesService _transaccionesService;

    public TransaccionesController(FinMindDbContext context, ITransaccionesService transaccionesService)
    {
        _context = context;
        _transaccionesService = transaccionesService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Transaccion>>> ObtenerTodas()
    {
        var transacciones = await _context.Transacciones
            .Include(t => t.Usuario)
            .Include(t => t.CuentaBancaria)
            .Include(t => t.Categoria)
            .OrderByDescending(t => t.Fecha)
            .ToListAsync();

        return Ok(transacciones);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Transaccion>> ObtenerPorId(Guid id)
    {
        var transaccion = await _context.Transacciones
            .Include(t => t.Usuario)
            .Include(t => t.CuentaBancaria)
            .Include(t => t.Categoria)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transaccion == null)
            throw new NotFoundException("No se ha encontrado la transacción.");

        return Ok(transaccion);
    }
    [HttpPost("crear")]
    public async Task<IActionResult> CrearManual([FromBody] CrearTransaccionManualRequestDto request)
    {
        var resultado = await _transaccionesService.CrearManualAsync(request);
        return Ok(resultado);
    }

    [HttpPut("modificar/{id}")]
    public async Task<IActionResult> Actualizar(Guid id, Transaccion transaccion)
    {
        if (id != transaccion.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var transaccionActual = await _context.Transacciones.FindAsync(id);
        if (transaccionActual == null)
            throw new NotFoundException("No se ha encontrado la transacción.");

        var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == transaccion.UsuarioId);
        if (!usuarioExiste)
            throw new BadRequestException("El usuario indicado no existe.");

        var cuentaExiste = await _context.CuentasBancarias.AnyAsync(c => c.Id == transaccion.CuentaBancariaId);
        if (!cuentaExiste)
            throw new BadRequestException("La cuenta indicada no existe.");

        if (transaccion.CategoriaId.HasValue)
        {
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == transaccion.CategoriaId.Value);
            if (!categoriaExiste)
                throw new BadRequestException("La categoría indicada no existe.");
        }

        transaccionActual.UsuarioId = transaccion.UsuarioId;
        transaccionActual.CuentaBancariaId = transaccion.CuentaBancariaId;
        transaccionActual.CategoriaId = transaccion.CategoriaId;
        transaccionActual.Importe = transaccion.Importe;
        transaccionActual.Moneda = transaccion.Moneda;
        transaccionActual.Tipo = transaccion.Tipo;
        transaccionActual.Origen = transaccion.Origen;
        transaccionActual.Fecha = transaccion.Fecha;
        transaccionActual.Descripcion = transaccion.Descripcion;
        transaccionActual.IdTransaccionExterna = transaccion.IdTransaccionExterna;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var transaccion = await _context.Transacciones.FindAsync(id);

        if (transaccion == null)
            throw new NotFoundException("No se ha encontrado la transacción.");

        _context.Transacciones.Remove(transaccion);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("sincronizar/{usuarioId:guid}")]
    public async Task<IActionResult> SincronizarDesdeTink(
    Guid usuarioId)
    {
        var resultado = await _transaccionesService.SincronizarDesdeTinkAsync(usuarioId);
        return Ok(resultado);
    }
    [HttpGet("obtener/{usuarioId:guid}")]
    public async Task<IActionResult> ObtenerPorUsuario(
       Guid usuarioId,
       [FromQuery] int? mes,
       [FromQuery] int? anio,
       [FromQuery] int? tipo,
       [FromQuery] string? texto,
       [FromQuery] int pagina = 1,
       [FromQuery] int tamanyo = 20)
    {
        var resultado = await _transaccionesService.ObtenerPorUsuarioAsync(
            usuarioId,
            mes,
            anio,
            tipo,
            texto,
            pagina,
            tamanyo);

        return Ok(resultado);
    }
}