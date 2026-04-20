using FinMind.DTO.IA;
using FinMind.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
[Route("api/ia")]
public class IAController : BaseController
{
    private readonly IIAFinanzasService _iaFinanzasService;

    public IAController(IIAFinanzasService iaFinanzasService)
    {
        _iaFinanzasService = iaFinanzasService;
    }

    [HttpPost("entrenar-modelo-categorias")]
    [Authorize]
    public async Task<IActionResult> EntrenarModeloCategorias([FromQuery] bool forzar = false, CancellationToken cancellationToken = default)
    {
        var resultado = await _iaFinanzasService.EntrenarModeloCategoriasAsync(forzar, cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("sugerir-categoria")]
    [Authorize]
    public async Task<IActionResult> SugerirCategoria([FromBody] SugerenciaCategoriaRequestDto request, CancellationToken cancellationToken = default)
    {
        request.UsuarioId ??= ObtenerUsuarioId();
        var resultado = await _iaFinanzasService.SugerirCategoriaAsync(request, cancellationToken);
        return Ok(resultado);
    }
}
