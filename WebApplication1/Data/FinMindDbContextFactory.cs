using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinMind.Data;

public class FinMindDbContextFactory : IDesignTimeDbContextFactory<FinMindDbContext>
{
    public FinMindDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<FinMindDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("FINMIND_DESIGNTIME_CONNECTION")
            ?? configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No se ha encontrado ConnectionStrings:DefaultConnection para ejecutar herramientas de EF.");
        }

        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));

        return new FinMindDbContext(optionsBuilder.Options);
    }
}
