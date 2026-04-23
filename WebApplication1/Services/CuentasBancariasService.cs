using FinMind.Common.Exceptions;
using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Services;

public class CuentasBancariasService : ICuentasBancariasService
{
    private readonly FinMindDbContext _context;

    public CuentasBancariasService(FinMindDbContext context)
    {
        _context = context;
    }

    public async Task<CuentaSeleccionadaResponseDto?> ObtenerCuentaPorUsuarioAsync(Guid usuarioId)
    {
        return await _context.CuentasBancarias
            .Where(c => c.UsuarioId == usuarioId && c.Activa)
            .OrderByDescending(c => c.FechaUltimaSincronizacion)
            .Select(c => new CuentaSeleccionadaResponseDto
            {
                Id = c.Id,
                IdCuentaExterna = c.IdCuentaExterna,
                Nombre = c.Nombre,
                Banco = c.Banco,
                Iban = c.Iban,
                Moneda = c.Moneda,
                Tipo = c.Tipo,
                FechaUltimaSincronizacion = c.FechaUltimaSincronizacion,
                SaldoActual = c.SaldoActual
            })
            .FirstOrDefaultAsync();
    }

    public async Task ActualizarCuentaAsync(Guid id, CuentaBancaria cuenta)
    {
        if (id != cuenta.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var cuentaDb = await _context.CuentasBancarias.FindAsync(id);

        if (cuentaDb == null)
            throw new NotFoundException("No se ha encontrado la cuenta bancaria.");

        cuentaDb.Nombre = cuenta.Nombre;
        cuentaDb.Iban = cuenta.Iban;
        cuentaDb.Banco = cuenta.Banco;
        cuentaDb.BIC = cuenta.BIC;
        cuentaDb.Moneda = cuenta.Moneda;
        cuentaDb.Tipo = cuenta.Tipo;
        cuentaDb.Activa = cuenta.Activa;

        await _context.SaveChangesAsync();
    }

    public async Task EliminarCuentaAsync(Guid id)
    {
        var cuenta = await _context.CuentasBancarias.FindAsync(id);

        if (cuenta == null)
            throw new NotFoundException("No se ha encontrado la cuenta bancaria.");

        _context.CuentasBancarias.Remove(cuenta);
        await _context.SaveChangesAsync();
    }
}
