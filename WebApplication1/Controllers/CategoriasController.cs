using FindMind.Common.Exceptions;
using FindMind.Data;
using FindMind.Interfaces;
using FindMind.Models.Enitdades;
using FindMind.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly FindMindDbContext _context;
    private readonly ICategoriaSeedService _categoriaSeedService;

    public CategoriasController(FindMindDbContext context, ICategoriaSeedService categoriaSeedService)
    {
        _context = context;
        _categoriaSeedService = categoriaSeedService;

    }

    [HttpPost("importar-tink")]
    public async Task<IActionResult> ImportarCategoriasTink([FromQuery] string locale = "es_ES")
    {
        var total = await _categoriaSeedService.ImportarCategoriasDesdeTinkAsync(locale);

        return Ok(new
        {
            mensaje = "Categorías importadas correctamente",
            totalInsertadas = total,
            locale
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Categoria>>> ObtenerTodas()
    {
        var categorias = await _context.Categorias
            .Include(c => c.Usuario)
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Categoria>> ObtenerPorId(Guid id)
    {
        var categoria = await _context.Categorias
            .Include(c => c.Usuario)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        return Ok(categoria);
    }

    [HttpGet("usuario/{usuarioId}")]
    public async Task<ActionResult<IEnumerable<Categoria>>> ObtenerPorUsuario(Guid usuarioId)
    {
        var categorias = await _context.Categorias
            .Where(c => c.EsSistema || c.UsuarioId == usuarioId)
            .ToListAsync();

        return Ok(categorias);
    }

    [HttpPost]
    public async Task<ActionResult<Categoria>> Crear(Categoria categoria)
    {
        if (!categoria.EsSistema && categoria.UsuarioId == null)
            throw new BadRequestException("Las categorías no de sistema deben tener UsuarioId.");

        if (categoria.UsuarioId.HasValue)
        {
            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == categoria.UsuarioId.Value);
            if (!usuarioExiste)
                throw new BadRequestException("El usuario indicado no existe.");
        }

        categoria.Id = Guid.NewGuid();
        categoria.FechaCreacion = DateTime.UtcNow;

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObtenerPorId), new { id = categoria.Id }, categoria);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(Guid id, Categoria categoria)
    {
        if (id != categoria.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var categoriaActual = await _context.Categorias.FindAsync(id);
        if (categoriaActual == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        if (!categoria.EsSistema && categoria.UsuarioId == null)
            throw new BadRequestException("Las categorías no de sistema deben tener UsuarioId.");

        if (categoria.UsuarioId.HasValue)
        {
            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == categoria.UsuarioId.Value);
            if (!usuarioExiste)
                throw new BadRequestException("El usuario indicado no existe.");
        }

        categoriaActual.UsuarioId = categoria.UsuarioId;
        categoriaActual.Nombre = categoria.Nombre;
        categoriaActual.Color = categoria.Color;
        categoriaActual.Icono = categoria.Icono;
        categoriaActual.EsSistema = categoria.EsSistema;
        categoriaActual.Archivada = categoria.Archivada;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var categoria = await _context.Categorias.FindAsync(id);

        if (categoria == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}