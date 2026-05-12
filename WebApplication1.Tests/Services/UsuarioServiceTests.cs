using FinMind.Data;
using FinMind.DTO.Autenticacion;
using FinMind.Models;
using FinMind.Models.Enitdades;
using FinMind.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinMind.Tests.Services;

public class UsuarioServiceTests
{
    [Fact]
    public async Task CambiarPasswordAsync_NuevaPasswordIgualActual_LanzaInvalidOperationException()
    {
        await using var context = CrearContextoEnMemoria();
        var passwordHasher = new PasswordHasher<Usuario>();
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = "password@finmind.dev",
            Nombre = "Password",
            MonedaPreferida = "EUR",
            Idioma = "es",
            Activo = true,
            Rol = RolesSistema.Usuario
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, "Password!123");

        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var service = new UsuarioService(
            context,
            passwordHasher,
            Options.Create(new JwtOptions
            {
                Key = "ClaveSuperSeguraDePruebas123!ClaveSuperSegura",
                Issuer = "FinMind.Tests",
                Audience = "FinMind.Tests",
                ExpirationMinutes = 60
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CambiarPasswordAsync(
            usuario.Id,
            new CambiarPasswordDto
            {
                PasswordActual = "Password!123",
                PasswordNueva = "Password!123"
            }));
    }

    private static FinMindDbContext CrearContextoEnMemoria()
    {
        var options = new DbContextOptionsBuilder<FinMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FinMindDbContext(options);
    }
}
