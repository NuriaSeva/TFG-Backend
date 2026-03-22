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
}