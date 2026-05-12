using FinMind.Data;
using FinMind.DTO;
using FinMind.DTO.Banking;
using FinMind.DTO.IA;
using FinMind.Interfaces;
using FinMind.Models.Enitdades;
using FinMind.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinMind.Tests.Services;

public class TransaccionesServiceTests
{
    [Fact]
    public async Task CrearManualAsync_RequestValido_GuardaTransaccionManual()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "mov@finmind.dev",
            PasswordHash = "hash",
            Nombre = "Mov",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        context.Categorias.Add(new Categoria
        {
            Id = categoriaId,
            UsuarioId = usuarioId,
            Nombre = "Comida",
            Tipo = TipoCategoria.Gasto,
            EsSistema = false
        });

        await context.SaveChangesAsync();

        var service = CrearServicio(context);

        var resultado = await service.CrearManualAsync(new CrearTransaccionManualRequestDto
        {
            CategoriaId = categoriaId,
            Importe = 45.75m,
            Tipo = 2,
            Fecha = DateTime.UtcNow.Date,
            Descripcion = "Cena",
            Moneda = "eur"
        }, usuarioId);

        Assert.Equal(usuarioId, resultado.UsuarioId);
        Assert.Equal(categoriaId, resultado.CategoriaId);
        Assert.Equal(45.75m, resultado.Importe);
        Assert.Equal("EUR", resultado.Moneda);
        Assert.Equal((int)OrigenTransaccion.Manual, resultado.Origen);

        var enBd = await context.Transacciones.SingleAsync(t => t.Id == resultado.Id);
        Assert.Equal(usuarioId, enBd.UsuarioId);
        Assert.Equal(categoriaId, enBd.CategoriaId);
        Assert.Equal(TipoTransaccion.Gasto, enBd.Tipo);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_FiltrosTipoYTexto_DevuelveSoloCoincidencias()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();

        context.Usuarios.AddRange(
            new Usuario
            {
                Id = usuarioId,
                Email = "filtro@finmind.dev",
                PasswordHash = "hash",
                Nombre = "Filtro",
                MonedaPreferida = "EUR",
                Idioma = "es"
            },
            new Usuario
            {
                Id = otroUsuarioId,
                Email = "otro@finmind.dev",
                PasswordHash = "hash",
                Nombre = "Otro",
                MonedaPreferida = "EUR",
                Idioma = "es"
            });

        context.Transacciones.AddRange(
            new Transaccion
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Importe = 20m,
                Moneda = "EUR",
                Tipo = TipoTransaccion.Gasto,
                Origen = OrigenTransaccion.Manual,
                Fecha = new DateTime(2026, 4, 10),
                Descripcion = "Supermercado"
            },
            new Transaccion
            {
                Id = Guid.NewGuid(),
                UsuarioId = usuarioId,
                Importe = 1500m,
                Moneda = "EUR",
                Tipo = TipoTransaccion.Ingreso,
                Origen = OrigenTransaccion.Manual,
                Fecha = new DateTime(2026, 4, 5),
                Descripcion = "Nomina"
            },
            new Transaccion
            {
                Id = Guid.NewGuid(),
                UsuarioId = otroUsuarioId,
                Importe = 99m,
                Moneda = "EUR",
                Tipo = TipoTransaccion.Gasto,
                Origen = OrigenTransaccion.Manual,
                Fecha = new DateTime(2026, 4, 11),
                Descripcion = "Supermercado otro usuario"
            });

        await context.SaveChangesAsync();

        var service = CrearServicio(context);

        var resultado = await service.ObtenerPorUsuarioAsync(
            usuarioId,
            mes: 4,
            anio: 2026,
            tipo: 2,
            texto: "Super",
            pagina: 1,
            tamanyo: 20);

        Assert.Equal(1, resultado.Total);
        Assert.Single(resultado.Items);
        Assert.Equal("Supermercado", resultado.Items[0].Descripcion);
        Assert.Equal((int)TipoTransaccion.Gasto, resultado.Items[0].Tipo);
    }

    [Fact]
    public async Task CrearManualAsync_UsuarioNoExiste_LanzaInvalidOperationException()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();

        var service = CrearServicio(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearManualAsync(
            new CrearTransaccionManualRequestDto
            {
                Importe = 10m,
                Tipo = 2,
                Fecha = DateTime.UtcNow
            },
            usuarioId));
    }

    [Fact]
    public async Task CrearManualAsync_SinCategoria_AutocategorizaSiIADevuelveCategoria()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "ia@finmind.dev",
            PasswordHash = "hash",
            Nombre = "IA",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        context.Categorias.Add(new Categoria
        {
            Id = categoriaId,
            UsuarioId = usuarioId,
            Nombre = "Restaurantes",
            Tipo = TipoCategoria.Gasto,
            EsSistema = false
        });

        await context.SaveChangesAsync();

        var service = new TransaccionesService(
            context,
            new TinkBankingServiceFake(),
            new IAFinanzasServiceFake(categoriaId),
            NullLogger<TransaccionesService>.Instance);

        var resultado = await service.CrearManualAsync(new CrearTransaccionManualRequestDto
        {
            Importe = 22.5m,
            Tipo = 2,
            Fecha = DateTime.UtcNow,
            Descripcion = "Cena viernes"
        }, usuarioId);

        Assert.Equal(categoriaId, resultado.CategoriaId);
    }

    [Fact]
    public async Task CrearManualAsync_CuentaDeOtroUsuario_LanzaInvalidOperationException()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var cuentaId = Guid.NewGuid();

        context.Usuarios.AddRange(
            new Usuario
            {
                Id = usuarioId,
                Email = "usuario@finmind.dev",
                PasswordHash = "hash",
                Nombre = "Usuario",
                MonedaPreferida = "EUR",
                Idioma = "es"
            },
            new Usuario
            {
                Id = otroUsuarioId,
                Email = "otro-cuenta@finmind.dev",
                PasswordHash = "hash",
                Nombre = "Otro",
                MonedaPreferida = "EUR",
                Idioma = "es"
            });

        context.CuentasBancarias.Add(new CuentaBancaria
        {
            Id = cuentaId,
            UsuarioId = otroUsuarioId,
            Banco = "Banco Test",
            Nombre = "Cuenta otro usuario",
            Moneda = "EUR",
            Activa = true
        });

        await context.SaveChangesAsync();

        var service = CrearServicio(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CrearManualAsync(
            new CrearTransaccionManualRequestDto
            {
                CuentaBancariaId = cuentaId,
                Importe = 12m,
                Tipo = 2,
                Fecha = DateTime.UtcNow,
                Descripcion = "Compra"
            },
            usuarioId));
    }

    [Fact]
    public async Task CrearManualAsync_SiIAFalla_GuardaTransaccionSinCategoria()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "ia-falla@finmind.dev",
            PasswordHash = "hash",
            Nombre = "IA Falla",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        await context.SaveChangesAsync();

        var service = new TransaccionesService(
            context,
            new TinkBankingServiceFake(),
            new IAFinanzasServiceFake(fallarSugerencia: true),
            NullLogger<TransaccionesService>.Instance);

        var resultado = await service.CrearManualAsync(new CrearTransaccionManualRequestDto
        {
            Importe = 18m,
            Tipo = 2,
            Fecha = DateTime.UtcNow,
            Descripcion = "Compra sin categoria"
        }, usuarioId);

        Assert.Null(resultado.CategoriaId);

        var enBd = await context.Transacciones.SingleAsync(t => t.Id == resultado.Id);
        Assert.Null(enBd.CategoriaId);
    }

    [Fact]
    public async Task ObtenerPorUsuarioAsync_PaginacionInvalida_NormalizaValores()
    {
        await using var context = CrearContextoEnMemoria();
        var usuarioId = Guid.NewGuid();

        context.Usuarios.Add(new Usuario
        {
            Id = usuarioId,
            Email = "pagina@finmind.dev",
            PasswordHash = "hash",
            Nombre = "Pagina",
            MonedaPreferida = "EUR",
            Idioma = "es"
        });

        context.Transacciones.Add(new Transaccion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Importe = 5m,
            Moneda = "EUR",
            Tipo = TipoTransaccion.Gasto,
            Origen = OrigenTransaccion.Manual,
            Fecha = DateTime.UtcNow,
            Descripcion = "Movimiento"
        });

        await context.SaveChangesAsync();

        var service = CrearServicio(context);

        var resultado = await service.ObtenerPorUsuarioAsync(
            usuarioId,
            pagina: 0,
            tamanyo: 0);

        Assert.Equal(1, resultado.Pagina);
        Assert.Equal(20, resultado.Tamanyo);
        Assert.Equal(1, resultado.Total);
        Assert.Single(resultado.Items);
    }

    private static TransaccionesService CrearServicio(FinMindDbContext context)
    {
        return new TransaccionesService(
            context,
            new TinkBankingServiceFake(),
            new IAFinanzasServiceFake(),
            NullLogger<TransaccionesService>.Instance);
    }

    private static FinMindDbContext CrearContextoEnMemoria()
    {
        var options = new DbContextOptionsBuilder<FinMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FinMindDbContext(options);
    }

    private sealed class TinkBankingServiceFake : ITinkBankingService
    {
        public Task<object> GetLoginUrlAsync(string localUserId) => throw new NotImplementedException();
        public Task<AccountCheckCallbackResultDto> HandleAccountCheckCallbackAsync(string localUserId, IDictionary<string, string> queryParams) => throw new NotImplementedException();
        public Task<AccountCheckCallbackResultDto?> GetLastAccountCheckResultAsync(string localUserId) => throw new NotImplementedException();
        public Task<string> GetClientAccessTokenAsync() => throw new NotImplementedException();
        public Task<string> GetAccountVerificationReportRawAsync(string reportId) => throw new NotImplementedException();
        public Task<CuentaSeleccionadaResponseDto> GuardarCuentaDesdeAccountCheckAsync(Guid usuarioId, string reportId) => throw new NotImplementedException();
        public Task<CuentaSeleccionadaResponseDto> ProcesarCallbackYGuardarCuentaAsync(string localUserId, IDictionary<string, string> queryParams) => throw new NotImplementedException();
        public Task<object> GetTransactionsLoginUrlAsync(string localUserId) => throw new NotImplementedException();
        public Task GuardarTokensTransactionsAsync(Guid usuarioId, string code) => throw new NotImplementedException();
        public Task<string> ObtenerAccessTokenVigenteAsync(Guid usuarioId) => throw new NotImplementedException();
        public Task<string> GetTransactionsRawAsync(Guid usuarioId, string? cuentaExternaId, Guid idCuenta) => throw new NotImplementedException();
        public Task DesvincularCuentaAsync(Guid usuarioId) => throw new NotImplementedException();
    }

    private sealed class IAFinanzasServiceFake : IIAFinanzasService
    {
        private readonly Guid? _categoriaId;
        private readonly bool _fallarSugerencia;

        public IAFinanzasServiceFake(Guid? categoriaId = null, bool fallarSugerencia = false)
        {
            _categoriaId = categoriaId;
            _fallarSugerencia = fallarSugerencia;
        }

        public Task<EntrenamientoModeloCategoriasResponseDto> EntrenarModeloCategoriasAsync(bool forzar = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<SugerenciaCategoriaResponseDto> SugerirCategoriaAsync(SugerenciaCategoriaRequestDto request, CancellationToken cancellationToken = default)
        {
            if (_fallarSugerencia)
            {
                throw new InvalidOperationException("Fallo simulado de IA.");
            }

            return Task.FromResult(new SugerenciaCategoriaResponseDto
            {
                MejorSugerencia = _categoriaId.HasValue
                    ? new CategoriaSugeridaDto
                    {
                        CategoriaId = _categoriaId.Value,
                        CategoriaNombre = "Sugerida test",
                        Confianza = 0.95m,
                        Fuente = "test"
                    }
                    : null,
                Alternativas = new List<CategoriaSugeridaDto>(),
                Confianza = 0m,
                Fuente = "test",
                RequiereConfirmacion = true,
                UmbralAutoasignacion = 0.7m
            });
        }
    }
}
