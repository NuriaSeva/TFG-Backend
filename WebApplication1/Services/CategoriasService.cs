using FinMind.Common.Exceptions;
using FinMind.Data;
using FinMind.DTO;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Services;

public class CategoriasService : ICategoriasService
{
    private readonly FinMindDbContext _context;
    private readonly ICategoriaSeedService _categoriaSeedService;

    public CategoriasService(FinMindDbContext context, ICategoriaSeedService categoriaSeedService)
    {
        _context = context;
        _categoriaSeedService = categoriaSeedService;
    }

    public async Task<int> ImportarCategoriasTinkAsync(string locale = "es_ES")
    {
        return await _categoriaSeedService.ImportarCategoriasDesdeTinkAsync(locale);
    }

    public async Task<CategoriaResponseDto?> ObtenerPorIdAsync(Guid id)
    {
        return await _context.Categorias
            .Where(c => c.Id == id)
            .Select(c => new CategoriaResponseDto
            {
                Id = c.Id,
                UsuarioId = c.UsuarioId,
                Nombre = c.Nombre,
                Tipo = (int)c.Tipo,
                Color = c.Color,
                Icono = c.Icono,
                EsSistema = c.EsSistema,
                Archivada = c.Archivada,
                FechaCreacion = c.FechaCreacion
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<CategoriaResponseDto>> ObtenerPorUsuarioAsync(Guid usuarioId)
    {
        return await _context.Categorias
            .Where(c => c.EsSistema || c.UsuarioId == usuarioId)
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaResponseDto
            {
                Id = c.Id,
                UsuarioId = c.UsuarioId,
                Nombre = c.Nombre,
                Tipo = (int)c.Tipo,
                Color = c.Color,
                Icono = c.Icono,
                EsSistema = c.EsSistema,
                Archivada = c.Archivada,
                FechaCreacion = c.FechaCreacion
            })
            .ToListAsync();
    }

    public async Task<CategoriaResponseDto> CrearAsync(Categoria categoria, Guid usuarioId)
    {
        categoria.UsuarioId = usuarioId;
        NormalizarCategoria(categoria);

        await ValidarCategoriaAsync(categoria);

        categoria.Id = Guid.NewGuid();
        categoria.FechaCreacion = DateTime.UtcNow;

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return new CategoriaResponseDto
        {
            Id = categoria.Id,
            UsuarioId = categoria.UsuarioId,
            Nombre = categoria.Nombre,
            Tipo = (int)categoria.Tipo,
            Color = categoria.Color,
            Icono = categoria.Icono,
            EsSistema = categoria.EsSistema,
            Archivada = categoria.Archivada,
            FechaCreacion = categoria.FechaCreacion
        };
    }

    public async Task ActualizarAsync(Guid id, Categoria categoria)
    {
        if (id != categoria.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var categoriaActual = await _context.Categorias.FindAsync(id);
        if (categoriaActual == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        NormalizarCategoria(categoria);

        await ValidarCategoriaAsync(categoria, id);

        if (categoriaActual.EsSistema != categoria.EsSistema)
            throw new BadRequestException("No se puede cambiar el tipo de categoría entre sistema y usuario.");

        categoriaActual.UsuarioId = categoria.UsuarioId;
        categoriaActual.Nombre = categoria.Nombre;
        categoriaActual.Color = categoria.Color;
        categoriaActual.Icono = categoria.Icono;
        categoriaActual.EsSistema = categoria.EsSistema;
        categoriaActual.Tipo = categoria.Tipo;
        categoriaActual.Archivada = categoria.Archivada;

        await _context.SaveChangesAsync();
    }

    public async Task EliminarAsync(Guid id, Guid usuarioId)
    {
        var categoria = await _context.Categorias
            .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

        if (categoria == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        if (categoria.EsSistema)
            throw new BadRequestException("No se puede eliminar una categoría del sistema.");

        var transaccionesAsociadas = await _context.Transacciones
            .Where(t => t.UsuarioId == usuarioId && t.CategoriaId == id)
            .ToListAsync();

        if (transaccionesAsociadas.Count > 0)
        {
            foreach (var transaccion in transaccionesAsociadas)
            {
                transaccion.CategoriaId = null;
            }
        }

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
    }

    public async Task<int> ObtenerImpactoEliminacionAsync(Guid id, Guid usuarioId)
    {
        var categoria = await _context.Categorias
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId);

        if (categoria == null)
            throw new NotFoundException("No se ha encontrado la categoría.");

        if (categoria.EsSistema)
            throw new BadRequestException("No se puede eliminar una categoría del sistema.");

        return await _context.Transacciones
            .CountAsync(t => t.UsuarioId == usuarioId && t.CategoriaId == id);
    }

    private static void NormalizarCategoria(Categoria categoria)
    {
        categoria.Nombre = (categoria.Nombre ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(categoria.Color))
            categoria.Color = categoria.Color.Trim();

        if (!string.IsNullOrWhiteSpace(categoria.Icono))
            categoria.Icono = categoria.Icono.Trim();
    }

    private async Task ValidarCategoriaAsync(Categoria categoria, Guid? categoriaIdExcluir = null)
    {
        if (string.IsNullOrWhiteSpace(categoria.Nombre))
            throw new BadRequestException("El nombre de la categoría es obligatorio.");

        if (categoria.Nombre.Length > 100)
            throw new BadRequestException("El nombre de la categoría no puede superar los 100 caracteres.");

        if (!Enum.IsDefined(typeof(TipoCategoria), categoria.Tipo))
            throw new BadRequestException("El tipo de categoría no es válido.");

        if (!categoria.EsSistema && categoria.UsuarioId == null)
            throw new BadRequestException("Las categorías no de sistema deben tener UsuarioId.");

        if (categoria.EsSistema && categoria.UsuarioId != null)
            throw new BadRequestException("Las categorías del sistema no deben tener UsuarioId.");

        if (categoria.UsuarioId.HasValue)
        {
            var usuarioExiste = await _context.Usuarios.AnyAsync(u => u.Id == categoria.UsuarioId.Value);
            if (!usuarioExiste)
                throw new BadRequestException("El usuario indicado no existe.");
        }

        var nombreNormalizado = categoria.Nombre.Trim().ToLower();

        bool existeDuplicada;

        if (categoria.EsSistema)
        {
            existeDuplicada = await _context.Categorias.AnyAsync(c =>
                c.Id != (categoriaIdExcluir ?? Guid.Empty) &&
                c.EsSistema &&
                c.Tipo == categoria.Tipo &&
                c.Nombre.ToLower() == nombreNormalizado);
        }
        else
        {
            existeDuplicada = await _context.Categorias.AnyAsync(c =>
                c.Id != (categoriaIdExcluir ?? Guid.Empty) &&
                !c.EsSistema &&
                c.UsuarioId == categoria.UsuarioId &&
                c.Tipo == categoria.Tipo &&
                c.Nombre.ToLower() == nombreNormalizado);
        }

        if (existeDuplicada)
            throw new BadRequestException("Ya existe una categoría con ese nombre y tipo.");
    }
}
