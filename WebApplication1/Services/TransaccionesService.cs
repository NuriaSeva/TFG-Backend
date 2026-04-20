using FinMind.Data;
using FinMind.DTO;
using FinMind.DTO.Banking;
using FinMind.DTO.IA;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FinMind.Services;

public class TransaccionesService : ITransaccionesService
{
    private readonly FinMindDbContext _context;
    private readonly ITinkBankingService _tinkBankingService;
    private readonly IIAFinanzasService _iaFinanzasService;

    public TransaccionesService(
        FinMindDbContext context,
        ITinkBankingService tinkBankingService,
        IIAFinanzasService iaFinanzasService)
    {
        _context = context;
        _tinkBankingService = tinkBankingService;
        _iaFinanzasService = iaFinanzasService;
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

        var query = ConstruirConsultaUsuario(usuarioId, mes, anio, tipo, texto, exportarTodo: false);

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

    public async Task<byte[]> ExportarCsvAsync(
        Guid usuarioId,
        int? mes = null,
        int? anio = null,
        int? tipo = null,
        string? texto = null,
        bool exportarTodo = false)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var culture = CultureInfo.GetCultureInfo("es-ES");

        var items = await ConstruirConsultaUsuario(usuarioId, mes, anio, tipo, texto, exportarTodo)
            .OrderByDescending(t => t.Fecha)
            .ThenByDescending(t => t.FechaCreacion)
            .Select(t => new
            {
                t.Fecha,
                t.Descripcion,
                Categoria = t.Categoria != null ? t.Categoria.Nombre : null,
                t.Tipo,
                t.Importe,
                t.Moneda,
                t.Origen,
                Cuenta = t.CuentaBancaria != null ? t.CuentaBancaria.Nombre : null,
                Banco = t.CuentaBancaria != null ? t.CuentaBancaria.Banco : null
            })
            .ToListAsync();

        var csv = new StringBuilder();
        csv.Append('﻿');
        csv.AppendLine("sep=;");
        csv.AppendLine("Fecha;Descripción;Categoría;Tipo;Importe;Moneda;Origen;Cuenta;Banco");

        foreach (var item in items)
        {
            var importeConSigno = item.Tipo == TipoTransaccion.Gasto
                ? -item.Importe
                : item.Importe;

            csv.Append(EscaparCsv(item.Fecha.ToString("dd/MM/yyyy", culture)));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Descripcion ?? string.Empty));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Categoria ?? "Sin categoría"));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Tipo == TipoTransaccion.Ingreso ? "Ingreso" : "Gasto"));
            csv.Append(';');
            csv.Append(EscaparCsv(importeConSigno.ToString("N2", culture)));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Moneda ?? "EUR"));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Origen == OrigenTransaccion.Manual ? "Manual" : "Banco"));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Cuenta ?? string.Empty));
            csv.Append(';');
            csv.Append(EscaparCsv(item.Banco ?? string.Empty));
            csv.AppendLine();
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<ResultadoSincronizacionTransaccionesDto> SincronizarDesdeTinkAsync(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        var conexion = await _context.ConexionesBancarias
            .FirstOrDefaultAsync(c =>
                c.UsuarioId == usuarioId &&
                c.Proveedor == ProveedorBancario.Tink);

        if (conexion == null)
            throw new InvalidOperationException("No existe una conexión bancaria activa para el usuario.");

        var cuenta = await _context.CuentasBancarias
            .FirstOrDefaultAsync(c =>
                c.UsuarioId == usuarioId &&
                c.Activa &&
                c.ConexionBancariaId == conexion.Id);

        if (cuenta == null)
            throw new InvalidOperationException("No existe una cuenta bancaria activa para el usuario.");

        var sincronizacion = new SincronizacionBancaria
        {
            Id = Guid.NewGuid(),
            ConexionBancariaId = conexion.Id,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoSincronizacion.Correcta,
            NumeroMovimientosImportados = 0
        };

        _context.SincronizacionesBancarias.Add(sincronizacion);
        await _context.SaveChangesAsync();

        try
        {
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
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        t.UsuarioId == usuarioId &&
                        t.Proveedor == ProveedorTransaccion.Tink &&
                        t.IdTransaccionExterna == item.Id);

                if (existente != null)
                {
                    resultado.Ignoradas++;
                    continue;
                }

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

                await IntentarAutocategorizarAsync(nueva, usuarioId);

                _context.Transacciones.Add(nueva);
                resultado.Nuevas++;
            }

            var ahora = DateTime.UtcNow;

            cuenta.FechaUltimaSincronizacion = ahora;
            conexion.FechaUltimaSincronizacion = ahora;

            sincronizacion.FechaFin = ahora;
            sincronizacion.Estado = EstadoSincronizacion.Correcta;
            sincronizacion.NumeroMovimientosImportados = resultado.Nuevas;
            sincronizacion.MensajeError = null;

            await _context.SaveChangesAsync();

            return resultado;
        }
        catch (Exception ex)
        {
            sincronizacion.FechaFin = DateTime.UtcNow;
            sincronizacion.Estado = EstadoSincronizacion.Error;
            sincronizacion.MensajeError = ex.Message;
            sincronizacion.NumeroMovimientosImportados = 0;

            await _context.SaveChangesAsync();

            throw;
        }
    }

    private IQueryable<Transaccion> ConstruirConsultaUsuario(
        Guid usuarioId,
        int? mes,
        int? anio,
        int? tipo,
        string? texto,
        bool exportarTodo)
    {
        var query = _context.Transacciones
            .AsNoTracking()
            .Where(t => t.UsuarioId == usuarioId);

        if (exportarTodo)
            return query;

        if (anio.HasValue)
        {
            query = query.Where(t => t.Fecha.Year == anio.Value);
        }

        if (mes.HasValue)
        {
            query = query.Where(t => t.Fecha.Month == mes.Value);
        }

        if (tipo.HasValue && (tipo == 1 || tipo == 2))
        {
            var tipoEnum = tipo == 1
                ? TipoTransaccion.Ingreso
                : TipoTransaccion.Gasto;

            query = query.Where(t => t.Tipo == tipoEnum);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var textoNormalizado = texto.Trim();

            query = query.Where(t =>
                t.Descripcion != null &&
                EF.Functions.Like(t.Descripcion, $"%{textoNormalizado}%"));
        }

        return query;
    }

    private static string EscaparCsv(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;

        var valorNormalizado = valor
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");

        if (valorNormalizado.Contains(';') || valorNormalizado.Contains('"'))
        {
            valorNormalizado = valorNormalizado.Replace("\"", "\"\"");
            return $"\"{valorNormalizado}\"";
        }

        return valorNormalizado;
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

    public async Task<TransaccionesUsuarioResponseDto> CrearManualAsync(CrearTransaccionManualRequestDto request, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El id del usuario es obligatorio.", nameof(usuarioId));

        if (request.Importe <= 0)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(request.Importe));

        if (request.Tipo != 1 && request.Tipo != 2)
            throw new ArgumentException("El tipo debe ser 1 (Ingreso) o 2 (Gasto).", nameof(request.Tipo));

        var usuarioExiste = await _context.Usuarios
            .AsNoTracking()
            .AnyAsync(u => u.Id == usuarioId);

        if (!usuarioExiste)
            throw new InvalidOperationException("El usuario indicado no existe.");

        if (request.CuentaBancariaId.HasValue)
        {
            var cuentaExiste = await _context.CuentasBancarias
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CuentaBancariaId.Value && c.UsuarioId == usuarioId);

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
            UsuarioId = usuarioId,
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

        await IntentarAutocategorizarAsync(nueva, usuarioId);

        _context.Transacciones.Add(nueva);
        await _context.SaveChangesAsync();

        return new TransaccionesUsuarioResponseDto
        {
            Id = nueva.Id,
            UsuarioId = nueva.UsuarioId,
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

    private async Task IntentarAutocategorizarAsync(Transaccion transaccion, Guid usuarioId)
    {
        if (transaccion.CategoriaId.HasValue)
            return;

        try
        {
            var sugerencia = await _iaFinanzasService.SugerirCategoriaAsync(
                new SugerenciaCategoriaRequestDto
                {
                    Descripcion = transaccion.Descripcion ?? string.Empty,
                    Importe = transaccion.Importe,
                    Tipo = (int)transaccion.Tipo,
                    UsuarioId = usuarioId
                });

            var mejor = sugerencia.MejorSugerencia;

            if (mejor?.CategoriaId.HasValue == true && !sugerencia.RequiereConfirmacion)
            {
                transaccion.CategoriaId = mejor.CategoriaId.Value;
            }
        }
        catch
        {
            // La transacción no debe fallar si la sugerencia IA no está disponible.
        }
    }
}
