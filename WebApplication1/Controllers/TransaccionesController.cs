using FinMind.Common.Exceptions;
using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using FinMind.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransaccionesController : BaseController
{
    private readonly FinMindDbContext _context;
    private readonly ITransaccionesService _transaccionesService;

    public TransaccionesController(FinMindDbContext context, ITransaccionesService transaccionesService)
    {
        _context = context;
        _transaccionesService = transaccionesService;
    }

    [HttpGet]
    [Authorize]
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
    [Authorize]
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
    [Authorize]
    public async Task<IActionResult> CrearManual([FromBody] CrearTransaccionManualRequestDto request)
    {
        var usuarioId =ObtenerUsuarioId();
        var resultado = await _transaccionesService.CrearManualAsync(request, usuarioId);
        return Ok(resultado);
    }

    [HttpPut("modificar/{id}")]
    [Authorize]
    public async Task<IActionResult> Actualizar(Guid id, ActualizarTransaccionRequestDto transaccion)
    {
        if (id != transaccion.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var transaccionActual = await _context.Transacciones.FindAsync(id);
        if (transaccionActual == null)
            throw new NotFoundException("No se ha encontrado la transacción.");

        if (transaccion.CategoriaId.HasValue)
        {
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == transaccion.CategoriaId.Value);
            if (!categoriaExiste)
                throw new BadRequestException("La categoría indicada no existe.");
        }
        var usuarioId = ObtenerUsuarioId();

        transaccionActual.UsuarioId = usuarioId;
        transaccionActual.CuentaBancariaId = transaccion.CuentaBancariaId;
        transaccionActual.CategoriaId = transaccion.CategoriaId;
        transaccionActual.Importe = transaccion.Importe;
        transaccionActual.Moneda = transaccion.Moneda;
        transaccionActual.Tipo = (TipoTransaccion)transaccion.Tipo;
        transaccionActual.Origen = (OrigenTransaccion)transaccion.Origen;
        transaccionActual.Fecha = transaccion.Fecha;
        transaccionActual.Descripcion = transaccion.Descripcion;
        transaccionActual.IdTransaccionExterna = transaccion.IdTransaccionExterna;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var transaccion = await _context.Transacciones.FindAsync(id);

        if (transaccion == null)
            throw new NotFoundException("No se ha encontrado la transacción.");

        _context.Transacciones.Remove(transaccion);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("sincronizar")]
    [Authorize]
    public async Task<IActionResult> SincronizarDesdeTink()
    {
        var usuarioId = ObtenerUsuarioId();
        var resultado = await _transaccionesService.SincronizarDesdeTinkAsync(usuarioId);
        return Ok(resultado);
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<IActionResult> ObtenerPorUsuario(
        [FromQuery] int? mes,
        [FromQuery] int? anio,
        [FromQuery] int? tipo,
        [FromQuery] string? texto,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanyo = 20)
    {
        var usuarioId = ObtenerUsuarioId();
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

    [HttpGet("exportar-csv")]
    [Authorize]
    public async Task<IActionResult> ExportarCsv(
        [FromQuery] int? mes,
        [FromQuery] int? anio,
        [FromQuery] int? tipo,
        [FromQuery] string? texto,
        [FromQuery] bool exportarTodo = false)
    {
        var usuarioId = ObtenerUsuarioId();
        var archivo = await _transaccionesService.ExportarCsvAsync(
            usuarioId,
            mes,
            anio,
            tipo,
            texto,
            exportarTodo);

        var sufijo = exportarTodo ? "todos" : "filtrados";
        var nombreArchivo = $"movimientos_{sufijo}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        return File(archivo, "text/csv; charset=utf-8", nombreArchivo);
    }
}
