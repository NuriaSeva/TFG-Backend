using FinMind.Data;
using FinMind.DTO;
using FinMind.DTO.Banking;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FinMind.Services;

public class TransaccionesService : ITransaccionesService
{
    private readonly FinMindDbContext _context;
    private readonly ITinkBankingService _tinkBankingService;

    public TransaccionesService(
        FinMindDbContext context,
        ITinkBankingService tinkBankingService)
    {
        _context = context;
        _tinkBankingService = tinkBankingService;
    }

    public async Task<PaginacionDTO<TransaccionesUsuarioResponseDto>> ObtenerPorUsuarioAsync(
    Guid usuarioId,
    int? mes = null,
    int? anio = null,
    int? tipo = null,
    string? texto = null,
    int pagina = 1,
    int tamanyo = 20)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        if (pagina < 1)
            pagina = 1;

        if (tamanyo < 1)
            tamanyo = 20;

        if (tamanyo > 100)
            tamanyo = 100;

        var query = _context.Transacciones
            .AsNoTracking()
            .Where(t => t.UsuarioId == usuarioId);

        if (anio.HasValue)
        {
            query = query.Where(t => t.Fecha.Year == anio.Value);
        }

        if (mes.HasValue)
        {
            query = query.Where(t => t.Fecha.Month == mes.Value);
        }

        if (tipo.HasValue)
        {
            TipoTransaccion tipoEnum= new TipoTransaccion();
            if (tipo == 1)
            {
                tipoEnum = TipoTransaccion.Ingreso;
            }
            else if (tipo == 2)
            {
                tipoEnum = TipoTransaccion.Gasto;
            }
         
            query = query.Where(t => t.Tipo == tipoEnum);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var textoNormalizado = texto.Trim();

            query = query.Where(t =>
                t.Descripcion != null &&
                EF.Functions.Like(t.Descripcion, $"%{textoNormalizado}%"));
        }

        var total = await query.CountAsync();

        var items = await query
         .OrderByDescending(t => t.Fecha)
         .ThenByDescending(t => t.FechaCreacion)
         .Skip((pagina - 1) * tamanyo)
         .Take(tamanyo)
         .Select(t => new TransaccionesUsuarioResponseDto
         {
             Id = t.Id,
             CuentaBancariaId = t.CuentaBancariaId,
             CategoriaId = t.CategoriaId,
             CategoriaNombre = t.Categoria != null ? t.Categoria.Nombre : null,
             Importe = t.Importe,
             Moneda = t.Moneda,
             Tipo = (int)t.Tipo,
             Origen = (int)t.Origen,
             Proveedor = (int)t.Proveedor,
             Fecha = t.Fecha,
             Descripcion = t.Descripcion,
             IdTransaccionExterna = t.IdTransaccionExterna
         })
         .ToListAsync();

        return new PaginacionDTO<TransaccionesUsuarioResponseDto>
        {
            Items = items,
            Total = total,
            Pagina = pagina,
            Tamanyo = tamanyo,
            TotalPaginas = (int)Math.Ceiling(total / (double)tamanyo)
        };
    }

    public async Task<ResultadoSincronizacionTransaccionesDto> SincronizarDesdeTinkAsync(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var cuenta = await _context.CuentasBancarias
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.Activa);

        if (cuenta == null)
            throw new InvalidOperationException("No existe una cuenta bancaria activa para el usuario.");

        var transaccionesTink = await _tinkBankingService.GetTransactionsRawAsync(
            usuarioId,
            cuenta.IdCuentaExterna,
            cuenta.Id);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var transacciones = JsonSerializer
            .Deserialize<TinkTransactionsResponseDto>(transaccionesTink, options)?
            .Transactions ?? new List<TinkTransactionDto>();

        var resultado = new ResultadoSincronizacionTransaccionesDto
        {
            TotalRecibidas = transacciones.Count,
            Nuevas = 0,
            Ignoradas = 0
        };

        foreach (var item in transacciones)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                resultado.Ignoradas++;
                continue;
            }

            var existente = await _context.Transacciones
                .FirstOrDefaultAsync(t =>
                    t.UsuarioId == usuarioId &&
                    t.IdTransaccionExterna == item.Id);

            var fecha = ParsearFecha(item.Dates?.Booked);
            var importeOriginal = ParsearImporte(item.Amount);
            var moneda = item.Amount?.CurrencyCode ?? cuenta.Moneda ?? "EUR";

            var tipo = importeOriginal >= 0
                ? TipoTransaccion.Ingreso
                : TipoTransaccion.Gasto;

            var importe = Math.Abs(importeOriginal);

            var descripcion =
                item.Descriptions?.Display
                ?? item.Descriptions?.Original
                ?? "Movimiento bancario";

            if (existente == null)
            {
                var nueva = new Transaccion
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioId,
                    CuentaBancariaId = cuenta.Id,
                    CategoriaId = null,
                    Importe = importe,
                    Moneda = moneda,
                    Tipo = tipo,
                    Origen = OrigenTransaccion.Banco,
                    Proveedor = ProveedorTransaccion.Tink,
                    Fecha = fecha,
                    Descripcion = descripcion,
                    IdTransaccionExterna = item.Id,
                    FechaCreacion = DateTime.UtcNow
                };

                _context.Transacciones.Add(nueva);
                resultado.Nuevas++;
            }
        }

        await _context.SaveChangesAsync();

        return resultado;
    }

    private static DateTime ParsearFecha(string? fecha)
    {
        if (string.IsNullOrWhiteSpace(fecha))
            return DateTime.UtcNow;

        if (DateTime.TryParse(fecha, out var parsed))
            return parsed;

        return DateTime.UtcNow;
    }

    private static decimal ParsearImporte(TinkMoneyAmountDto? amount)
    {
        if (amount?.Value == null)
            return 0m;

        if (!long.TryParse(amount.Value.UnscaledValue, out var unscaled))
            return 0m;

        if (!int.TryParse(amount.Value.Scale, out var scale))
            scale = 0;

        return unscaled / (decimal)Math.Pow(10, scale);
    }
    public async Task<TransaccionesUsuarioResponseDto> CrearManualAsync(CrearTransaccionManualRequestDto request)
    {
        if (request.UsuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(request.UsuarioId));

        if (request.Importe <= 0)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(request.Importe));

        if (request.Tipo != 1 && request.Tipo != 2)
            throw new ArgumentException("El tipo debe ser 1 (Ingreso) o 2 (Gasto).", nameof(request.Tipo));

        var usuarioExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == request.UsuarioId);

        if (!usuarioExiste)
            throw new InvalidOperationException("El usuario indicado no existe.");

        if (request.CuentaBancariaId.HasValue)
        {
            var cuentaExiste = await _context.CuentasBancarias
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CuentaBancariaId.Value && c.UsuarioId == request.UsuarioId);

            if (!cuentaExiste)
                throw new InvalidOperationException("La cuenta indicada no existe o no pertenece al usuario.");
        }

        if (request.CategoriaId.HasValue)
        {
            var categoriaExiste = await _context.Categorias
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CategoriaId.Value);

            if (!categoriaExiste)
                throw new InvalidOperationException("La categoría indicada no existe.");
        }

        var tipoEnum = request.Tipo == 1
            ? TipoTransaccion.Ingreso
            : TipoTransaccion.Gasto;

        var nueva = new Transaccion
        {
            Id = Guid.NewGuid(),
            UsuarioId = request.UsuarioId,
            CuentaBancariaId = request.CuentaBancariaId,
            CategoriaId = request.CategoriaId,
            Importe = Math.Abs(request.Importe),
            Moneda = string.IsNullOrWhiteSpace(request.Moneda) ? "EUR" : request.Moneda.Trim().ToUpper(),
            Tipo = tipoEnum,
            Origen = OrigenTransaccion.Manual,
            Proveedor = ProveedorTransaccion.Ninguno,
            Fecha = request.Fecha,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion)
                ? "Movimiento manual"
                : request.Descripcion.Trim(),
            IdTransaccionExterna = null,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Transacciones.Add(nueva);
        await _context.SaveChangesAsync();

        return new TransaccionesUsuarioResponseDto
        {
            Id = nueva.Id,
            UsuarioId= nueva.UsuarioId,
            CuentaBancariaId = nueva.CuentaBancariaId,
            CategoriaId = nueva.CategoriaId,
            Importe = nueva.Importe,
            Moneda = nueva.Moneda,
            Tipo = (int)nueva.Tipo,
            Origen = (int)nueva.Origen,
            Proveedor = (int)nueva.Proveedor,
            Fecha = nueva.Fecha,
            Descripcion = nueva.Descripcion,
            IdTransaccionExterna = nueva.IdTransaccionExterna
        };
    }
}