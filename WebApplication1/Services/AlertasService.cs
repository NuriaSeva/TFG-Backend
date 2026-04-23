using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Services;

public class AlertasService : IAlertasService
{
    private readonly FinMindDbContext _context;

    public AlertasService(FinMindDbContext context)
    {
        _context = context;
    }

    public async Task<PaginacionDTO<AlertaResponseDto>> ObtenerAsync(Guid usuarioId, int pagina = 1, int tamanyo = 20)
    {
        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        if (!notificacionesActivas)
        {
            return new PaginacionDTO<AlertaResponseDto>
            {
                Items = new List<AlertaResponseDto>(),
                Total = 0,
                Pagina = pagina < 1 ? 1 : pagina,
                Tamanyo = tamanyo < 1 ? 20 : tamanyo,
                TotalPaginas = 0
            };
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

        return new PaginacionDTO<AlertaResponseDto>
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            Tamanyo = tamanyo,
            TotalPaginas = (int)Math.Ceiling(total / (double)tamanyo)
        };
    }

    public async Task<int> ObtenerNoLeidasTotalAsync(Guid usuarioId)
    {
        var notificacionesActivas = await SonNotificacionesActivasAsync(usuarioId);
        if (!notificacionesActivas)
        {
            return 0;
        }

        var soloCriticas = await SonNotificacionesSoloCriticasAsync(usuarioId);
        return await _context.Alertas.CountAsync(a =>
            a.UsuarioId == usuarioId &&
            !a.Leida &&
            (!soloCriticas || EsAlertaCritica(a.Tipo)));
    }

    public async Task<bool> MarcarLeidaAsync(Guid id, Guid usuarioId)
    {
        var alerta = await _context.Alertas.FirstOrDefaultAsync(a => a.Id == id && a.UsuarioId == usuarioId);

        if (alerta == null)
            return false;

        if (!alerta.Leida)
        {
            alerta.Leida = true;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task MarcarTodasLeidasAsync(Guid usuarioId)
    {
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

    private static bool EsAlertaCritica(TipoAlerta tipo)
    {
        return tipo == TipoAlerta.Prediccion ||
               tipo == TipoAlerta.PresupuestoSuperado ||
               tipo == TipoAlerta.ErrorSincronizacion;
    }
}
