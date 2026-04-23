using System.Net;
using System.Net.Http.Json;
using FinMind.DTO.Autenticacion;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinMind.Tests.Integration;

public class AutenticacionIntegrationTests : IClassFixture<FinMindApiFactory>
{
    private readonly HttpClient _client;

    public AutenticacionIntegrationTests(FinMindApiFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task Registro_ConPasswordDebil_DevuelveBadRequest()
    {
        var request = new RegistroUsuarioDto
        {
            Email = "ana@test.com",
            Password = "1234",
            Nombre = "Ana"
        };

        var response = await _client.PostAsJsonAsync("/api/autenticacion/registro", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InicioSesion_ConEmailInvalido_DevuelveBadRequest()
    {
        var request = new InicioSesionDto
        {
            Email = "correo-invalido",
            Password = "Password!123"
        };

        var response = await _client.PostAsJsonAsync("/api/autenticacion/inicio-sesion", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Registro_Valido_DevuelveToken()
    {
        var request = new RegistroUsuarioDto
        {
            Email = "maria@test.com",
            Password = "Password!123",
            Nombre = "Maria",
            Apellidos = "Lopez"
        };

        var response = await _client.PostAsJsonAsync("/api/autenticacion/registro", request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AutenticacionResponseDto>();

        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.UsuarioId);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
    }
}
