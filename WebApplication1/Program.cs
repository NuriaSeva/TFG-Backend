using FinMind.Data;
using FinMind.Interfaces;
using FinMind.Middleware;
using FinMind.Models;
using FinMind.Models.Enitdades;
using FinMind.Services;
using FinMind.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const string corsPolicy = "AllowIonicApp";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        policy
            .WithOrigins(
                "capacitor://localhost",
                "ionic://localhost",
                "http://localhost",
                "http://127.0.0.1")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type");
    });
});

var jwtOptions = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>();

if (jwtOptions is null && builder.Environment.IsEnvironment("Testing"))
{
    jwtOptions = new JwtOptions
    {
        Key = "ClaveSuperSeguraDePruebas123!ClaveSuperSegura",
        Issuer = "FinMind.Tests",
        Audience = "FinMind.Tests",
        ExpirationMinutes = 60
    };
}

if (jwtOptions is null)
{
    throw new InvalidOperationException("No se ha configurado Jwt");
}

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<FinMindDbContext>(options =>
        options.UseInMemoryDatabase("FinMindTests"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                          ?? throw new InvalidOperationException("No se encontró la cadena de conexión");

    builder.Services.AddDbContext<FinMindDbContext>(options =>
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<TinkOptions>(builder.Configuration.GetSection("Tink"));
builder.Services.Configure<IAOptions>(builder.Configuration.GetSection("IA"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddHttpClient<ITinkBankingService, TinkBankingService>();
builder.Services.AddHttpClient<ICategoriaSeedService, CategoriaSeedService>();

builder.Services.AddScoped<ITransaccionesService, TransaccionesService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAnaliticaPredictivaService, AnaliticaPredictivaService>();
builder.Services.AddScoped<IIAFinanzasService, IAFinanzasService>();
builder.Services.AddScoped<ICategoriasService, CategoriasService>();
builder.Services.AddScoped<IAlertasService, AlertasService>();
builder.Services.AddScoped<IConfiguracionUsuarioService, ConfiguracionUsuarioService>();
builder.Services.AddScoped<ICuentasBancariasService, CuentasBancariasService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();

builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FinMind API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce: Bearer {tu token JWT}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };

        options.Events = new JwtBearerEvents();
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors(corsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => "FinMind API funcionando correctamente");

app.MapGet("/health/db", async (FinMindDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();

        if (canConnect)
        {
            return Results.Ok(new
            {
                status = "ok",
                database = "connected"
            });
        }

        return Results.Problem(
            title: "Database connection failed",
            detail: "No se pudo conectar con la base de datos.",
            statusCode: 500);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database error",
            detail: ex.Message,
            statusCode: 500);
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
