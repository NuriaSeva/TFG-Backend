using FinMind.Data;
using FinMind.Common.Exceptions;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using FinMind.Services;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Tests.Services;

public class CategoriasServiceTests
{
    [Fact]
    public async Task CrearAsync_CategoriaValida_GuardaCategoriaNormalizada()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "test@finmind.dev",
            PasswordHash = "hash",
            Nombre = "Test",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });
        await context.SaveChangesAsync();

        var service = new CategoriasService(context, new CategoriaSeedServiceFake());

        var creada = await service.CrearAsync(new Categoria
        {
            Nombre = "  Supermercado  ",
            Tipo = TipoCategoria.Gasto,
            Color = "  #00FF00  ",
            Icono = "  cart  ",
            EsSistema = false,
            Archivada = false
        }, usuarioId);

        Assert.Equal("Supermercado", creada.Nombre);
        Assert.Equal("#00FF00", creada.Color);
        Assert.Equal("cart", creada.Icono);

        var enBd = await context.Categorias.SingleAsync(c => c.Id == creada.Id);
        Assert.Equal(usuarioId, enBd.UsuarioId);
        Assert.Equal("Supermercado", enBd.Nombre);
    }

    [Fact]
    public async Task EliminarAsync_CategoriaConTransacciones_DejaTransaccionesSinCategoria()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();
        var transaccionId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "delete@finmind.dev",
            PasswordHash = "hash",
            Nombre = "Delete",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        context.Categorias.Add(new Categoria
        {
            Id = categoriaId,
            UsuarioId = usuarioId,
            Nombre = "Temporal",
            Tipo = TipoCategoria.Gasto,
            EsSistema = false
        });

        context.Transacciones.Add(new Transaccion
        {
            Id = transaccionId,
            UsuarioId = usuarioId,
            CategoriaId = categoriaId,
            Importe = 25m,
            Moneda = "EUR",
            Tipo = TipoTransaccion.Gasto,
            Origen = OrigenTransaccion.Manual,
            Fecha = DateTime.UtcNow,
            Descripcion = "Compra"
        });

        await context.SaveChangesAsync();

        var service = new CategoriasService(context, new CategoriaSeedServiceFake());
        await service.EliminarAsync(categoriaId, usuarioId);

        var categoriaEliminada = await context.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId);
        var transaccion = await context.Transacciones.SingleAsync(t => t.Id == transaccionId);

        Assert.Null(categoriaEliminada);
        Assert.Null(transaccion.CategoriaId);
    }

    [Fact]
    public async Task CrearAsync_CategoriaDuplicadaMismoUsuarioYTipo_LanzaBadRequest()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "dup@finmind.dev",
            PasswordHash = "hash",
            Nombre = "Dup",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        context.Categorias.Add(new Categoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Nombre = "Supermercado",
            Tipo = TipoCategoria.Gasto,
            EsSistema = false,
            Archivada = false
        });

        await context.SaveChangesAsync();

        var service = new CategoriasService(context, new CategoriaSeedServiceFake());

        await Assert.ThrowsAsync<BadRequestException>(() => service.CrearAsync(new Categoria
        {
            Nombre = " supermercado ",
            Tipo = TipoCategoria.Gasto,
            EsSistema = false,
            Archivada = false
        }, usuarioId));
    }

    [Fact]
    public async Task ActualizarAsync_IdUrlDistintoIdBody_LanzaBadRequest()
    {
        await using var context = CrearContextoEnMemoria();
        var service = new CategoriasService(context, new CategoriaSeedServiceFake());

        await Assert.ThrowsAsync<BadRequestException>(() => service.ActualizarAsync(
            Guid.NewGuid(),
            new Categoria
            {
                Id = Guid.NewGuid(),
                Nombre = "Hogar",
                Tipo = TipoCategoria.Gasto,
                EsSistema = false
            }));
    }

    private static FinMindDbContext CrearContextoEnMemoria()
    {
        var options = new DbContextOptionsBuilder<FinMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FinMindDbContext(options);
    }

    private sealed class CategoriaSeedServiceFake : ICategoriaSeedService
    {
        public Task<int> ImportarCategoriasDesdeTinkAsync(string locale = "es_ES")
            => Task.FromResult(0);
    }
}
