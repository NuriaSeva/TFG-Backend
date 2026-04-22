using FinMind.Data;
using FinMind.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/alertas")]
public class AlertasController : BaseController
{
    private readonly FinMindDbContext _context;

    public AlertasController(FinMindDbContext context)
    {
        _context = context;
    }

    [HttpGet("obtener")]
    [Authorize]
    public async Task<IActionResult> Obtener(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanyo = 20)
    {
        var usuarioId = ObtenerUsuarioId();
        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        if (!notificacionesActivas)
        {
            return Ok(new PaginacionDTO<AlertaResponseDto>
            {
                Items = new List<AlertaResponseDto>(),
                Total = 0,
                Pagina = pagina < 1 ? 1 : pagina,
                Tamanyo = tamanyo < 1 ? 20 : tamanyo,
                TotalPaginas = 0
            });
        }

        if (pagina < 1) pagina = 1;
        if (tamanyo < 1) tamanyo = 20;
        if (tamanyo > 100) tamanyo = 100;

        var query = _context.Alertas
            .AsNoTracking()
            .Where(a => a.UsuarioId == usuarioId);

        var soloCriticas = await SonNotificacionesSoloCriticasAsync(usuarioId);
        if (soloCriticas)
        {
            query = query.Where(a => EsAlertaCritica(a.Tipo));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.FechaCreacion)
            .Skip((pagina - 1) * tamanyo)
            .Take(tamanyo)
            .Select(a => new AlertaResponseDto
            {
                Id = a.Id,
                Tipo = (int)a.Tipo,
                Titulo = a.Titulo,
                Mensaje = a.Mensaje,
                Leida = a.Leida,
                FechaCreacion = a.FechaCreacion
            })
            .ToListAsync();

        return Ok(new PaginacionDTO<AlertaResponseDto>
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            Tamanyo = tamanyo,
            TotalPaginas = (int)Math.Ceiling(total / (double)tamanyo)
        });
    }

    [HttpGet("no-leidas-total")]
    [Authorize]
    public async Task<IActionResult> ObtenerNoLeidasTotal()
    {
        var usuarioId = ObtenerUsuarioId();
        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        if (!notificacionesActivas)
        {
            return Ok(new { total = 0 });
        }

        var soloCriticas = await SonNotificacionesSoloCriticasAsync(usuarioId);
        var total = await _context.Alertas.CountAsync(a =>
            a.UsuarioId == usuarioId &&
            !a.Leida &&
            (!soloCriticas || EsAlertaCritica(a.Tipo)));
        return Ok(new { total });
    }

    [HttpPatch("{id}/leer")]
    [Authorize]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        var usuarioId = ObtenerUsuarioId();
        var alerta = await _context.Alertas.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId);

        if (alerta == null)
            return NotFound();

        if (!alerta.Leida)
        {
            alerta.Leida = true;
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPatch("leer-todas")]
    [Authorize]
    public async Task<IActionResult> MarcarTodasLeidas()
    {
        var usuarioId = ObtenerUsuarioId();
        var alertas = await _context.Alertas
            .Where(a => a.UsuarioId == usuarioId && !a.Leida)
            .ToListAsync();

        if (alertas.Count > 0)
        {
            foreach (var alerta in alertas)
            {
                alerta.Leida = true;
            }

            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    private async Task<bool> SonNotificacionesActivasAsync(Guid usuarioId)
    {
        var configuracion = await _context.ConfiguracionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        return configuracion?.NotificacionesActivas ?? true;
    }

    private async Task<bool> SonNotificacionesSoloCriticasAsync(Guid usuarioId)
    {
        var configuracion = await _context.ConfiguracionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        return configuracion?.NotificacionesSoloCriticas ?? false;
    }

    private static bool EsAlertaCritica(FinMind.Models.Enitdades.TipoAlerta tipo)
    {
        return tipo == FinMind.Models.Enitdades.TipoAlerta.Prediccion ||
               tipo == FinMind.Models.Enitdades.TipoAlerta.PresupuestoSuperado ||
               tipo == FinMind.Models.Enitdades.TipoAlerta.ErrorSincronizacion;
    }
}
