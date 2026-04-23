using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriasController : BaseController
{
    private readonly ICategoriasService _categoriasService;

    public CategoriasController(ICategoriasService categoriasService)
    {
        _categoriasService = categoriasService;
    }

    [HttpPost("importar-tink")]
    public async Task<IActionResult> ImportarCategoriasTink([FromQuery] string locale = "es_ES")
    {
        var total = await _categoriasService.ImportarCategoriasTinkAsync(locale);

        return Ok(new
        {
            mensaje = "Categorías importadas correctamente",
            totalInsertadas = total,
            locale
        });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<CategoriaResponseDto>> ObtenerPorId(Guid id)
    {
        var categoria = await _categoriasService.ObtenerPorIdAsync(id);

        if (categoria == null)
            return NotFound();

        return Ok(categoria);
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<ActionResult<List<CategoriaResponseDto>>> ObtenerPorUsuario()
    {
        var usuarioId = ObtenerUsuarioId();
        var categorias = await _categoriasService.ObtenerPorUsuarioAsync(usuarioId);

        return Ok(categorias);
    }

    [HttpPost("crear")]
    [Authorize]
    public async Task<ActionResult<CategoriaResponseDto>> Crear(Categoria categoria)
    {
        var usuarioId = ObtenerUsuarioId();
        var dto = await _categoriasService.CrearAsync(categoria, usuarioId);
        return Ok(dto);
    }

    [HttpPut("modificar/{id}")]
    [Authorize]
    public async Task<IActionResult> Actualizar(Guid id, Categoria categoria)
    {
        await _categoriasService.ActualizarAsync(id, categoria);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();
        await _categoriasService.EliminarAsync(id, usuarioId);
        return NoContent();
    }

    [HttpGet("{id}/impacto-eliminacion")]
    [Authorize]
    public async Task<IActionResult> ObtenerImpactoEliminacion(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();
        var movimientosSinCategoria = await _categoriasService.ObtenerImpactoEliminacionAsync(id, usuarioId);

        return Ok(new
        {
            movimientosSinCategoria
        });
    }
}
