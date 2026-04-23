using FinMind.Data;
using FinMind.DTO;
using FinMind.Services;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Tests.Services;

public class ConfiguracionUsuarioServiceTests
{
    [Fact]
    public async Task ObtenerAsync_CreaConfiguracionPorDefecto_SiNoExiste()
    {
        await using var context = CrearContextoEnMemoria();
        var service = new ConfiguracionUsuarioService(context);
        var usuarioId = Guid.NewGuid();

        var resultado = await service.ObtenerAsync(usuarioId);

        Assert.True(resultado.NotificacionesActivas);
        Assert.False(resultado.NotificacionesSoloCriticas);
        Assert.Equal(1, await context.ConfiguracionesUsuario.CountAsync());
    }

    [Fact]
    public async Task ActualizarNotificacionesAsync_ActualizaValoresYPersiste()
    {
        await using var context = CrearContextoEnMemoria();
        var service = new ConfiguracionUsuarioService(context);
        var usuarioId = Guid.NewGuid();

        _ = await service.ObtenerAsync(usuarioId);

        var resultado = await service.ActualizarNotificacionesAsync(
            usuarioId,
            new ActualizarNotificacionesRequestDto
            {
                NotificacionesActivas = false,
                NotificacionesSoloCriticas = true
            });

        Assert.False(resultado.NotificacionesActivas);
        Assert.True(resultado.NotificacionesSoloCriticas);

        var enBd = await context.ConfiguracionesUsuario.SingleAsync(c => c.UsuarioId == usuarioId);
        Assert.False(enBd.NotificacionesActivas);
        Assert.True(enBd.NotificacionesSoloCriticas);
    }

    private static FinMindDbContext CrearContextoEnMemoria()
    {
        var options = new DbContextOptionsBuilder<FinMindDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FinMindDbContext(options);
    }
}
