using FinMind.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinMind.Tests.Integration;

public sealed class FinMindApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var config = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "ClaveSuperSeguraDePruebas123!ClaveSuperSegura",
                ["Jwt:Issuer"] = "FinMind.Tests",
                ["Jwt:Audience"] = "FinMind.Tests",
                ["Jwt:ExpirationMinutes"] = "60"
            };

            configBuilder.AddInMemoryCollection(config);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinMindDbContext>>();
            services.AddDbContext<FinMindDbContext>(options =>
                options.UseInMemoryDatabase($"FinMindTests-{Guid.NewGuid()}"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinMindDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
