using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinMind.Data;
using FinMind.DTO.Autenticacion;
using FinMind.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinMind.Tests.Integration;

public class AdminIntegrationTests : IClassFixture<FinMindApiFactory>
{
    private readonly HttpClient _client;
    private readonly FinMindApiFactory _factory;

    public AdminIntegrationTests(FinMindApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    [Fact]
    public async Task AdminHealth_ConUsuarioNormal_DevuelveForbidden()
    {
        var token = await RegistrarYObtenerTokenAsync("usuario@test.com", "User Test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/admin/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminHealth_ConUsuarioAdmin_DevuelveOk()
    {
        const string email = "admin@test.com";
        const string password = "Password!123";
        var tokenRegistro = await RegistrarYObtenerTokenAsync(email, "Admin Test", password);
        Assert.False(string.IsNullOrWhiteSpace(tokenRegistro));

        await PromocionarAAdminAsync(email);
        var token = await IniciarSesionYObtenerTokenAsync(email, password);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/admin/health");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var estadoApi = document.RootElement.GetProperty("estadoApi").GetString();

        Assert.Equal("healthy", estadoApi);
    }

    [Fact]
    public async Task ActualizarRol_ComoAdmin_ActualizaRolUsuario()
    {
        const string adminEmail = "admin2@test.com";
        const string userEmail = "user2@test.com";
        const string password = "Password!123";

        await RegistrarYObtenerTokenAsync(adminEmail, "Admin 2", password);
        await RegistrarYObtenerTokenAsync(userEmail, "User 2", password);

        await PromocionarAAdminAsync(adminEmail);
        var adminToken = await IniciarSesionYObtenerTokenAsync(adminEmail, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var userId = await ObtenerUsuarioIdPorEmailAsync(userEmail);
        var response = await _client.PatchAsJsonAsync($"/api/admin/usuarios/{userId}/rol", new { rol = "Admin" });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinMindDbContext>();
        var usuarioActualizado = await db.Usuarios.FirstAsync(u => u.Id == userId);
        Assert.Equal(RolesSistema.Admin, usuarioActualizado.Rol);
    }

    [Fact]
    public async Task EndpointNoAdmin_ConUsuarioAdmin_DevuelveForbidden()
    {
        const string adminEmail = "admin3@test.com";
        const string password = "Password!123";

        await RegistrarYObtenerTokenAsync(adminEmail, "Admin 3", password);
        await PromocionarAAdminAsync(adminEmail);
        var token = await IniciarSesionYObtenerTokenAsync(adminEmail, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/autenticacion/perfil");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ComoAdmin_PermiteLoginConPasswordTemporal()
    {
        const string adminEmail = "admin4@test.com";
        const string userEmail = "user4@test.com";
        const string passwordAnterior = "Password!123";

        await RegistrarYObtenerTokenAsync(adminEmail, "Admin 4", passwordAnterior);
        await RegistrarYObtenerTokenAsync(userEmail, "User 4", passwordAnterior);

        await PromocionarAAdminAsync(adminEmail);
        var adminToken = await IniciarSesionYObtenerTokenAsync(adminEmail, passwordAnterior);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var userId = await ObtenerUsuarioIdPorEmailAsync(userEmail);
        var resetResponse = await _client.PostAsync($"/api/admin/usuarios/{userId}/reset-password", content: null);
        resetResponse.EnsureSuccessStatusCode();

        var json = await resetResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var passwordTemporal = document.RootElement.GetProperty("passwordTemporal").GetString();

        Assert.False(string.IsNullOrWhiteSpace(passwordTemporal));

        var loginAntiguo = await _client.PostAsJsonAsync("/api/autenticacion/inicio-sesion", new InicioSesionDto
        {
            Email = userEmail,
            Password = passwordAnterior
        });
        Assert.Equal(HttpStatusCode.Unauthorized, loginAntiguo.StatusCode);

        var loginNuevo = await _client.PostAsJsonAsync("/api/autenticacion/inicio-sesion", new InicioSesionDto
        {
            Email = userEmail,
            Password = passwordTemporal!
        });
        loginNuevo.EnsureSuccessStatusCode();

        var payloadLoginNuevo = await loginNuevo.Content.ReadFromJsonAsync<AutenticacionResponseDto>();
        Assert.NotNull(payloadLoginNuevo);
        Assert.True(payloadLoginNuevo!.DebeCambiarPassword);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payloadLoginNuevo.Token);
        var intentoPerfil = await _client.GetAsync("/api/autenticacion/perfil");
        Assert.Equal(HttpStatusCode.Forbidden, intentoPerfil.StatusCode);
    }

    private async Task<string> RegistrarYObtenerTokenAsync(string email, string nombre, string password = "Password!123")
    {
        var request = new RegistroUsuarioDto
        {
            Email = email,
            Password = password,
            Nombre = nombre
        };

        var response = await _client.PostAsJsonAsync("/api/autenticacion/registro", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AutenticacionResponseDto>();
        Assert.NotNull(payload);

        return payload!.Token;
    }

    private async Task PromocionarAAdminAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinMindDbContext>();

        var usuario = await db.Usuarios.FirstAsync(u => u.Email == email);
        usuario.Rol = RolesSistema.Admin;
        await db.SaveChangesAsync();
    }

    private async Task<string> IniciarSesionYObtenerTokenAsync(string email, string password)
    {
        var request = new InicioSesionDto
        {
            Email = email,
            Password = password
        };

        var response = await _client.PostAsJsonAsync("/api/autenticacion/inicio-sesion", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AutenticacionResponseDto>();
        Assert.NotNull(payload);
        return payload!.Token;
    }

    private async Task<Guid> ObtenerUsuarioIdPorEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinMindDbContext>();

        var usuario = await db.Usuarios.FirstAsync(u => u.Email == email);
        return usuario.Id;
    }
}
