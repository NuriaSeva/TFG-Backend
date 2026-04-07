using FinMind.Data;
using FinMind.Interfaces;
using FinMind.Middleware;
using FinMind.Models;
using FinMind.Models.Enitdades;
using FinMind.Services;
using FinMind.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;



var builder = WebApplication.CreateBuilder(args);

var corsPolicy = "AllowIonicApp";

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicy, policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});
var jwtOptions = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtOptions>() ?? throw new InvalidOperationException("No se ha configurado Jwt");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new InvalidOperationException("No se encontró la cadena de conexión");

builder.Services.AddDbContext<FinMindDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<TinkOptions>(
    builder.Configuration.GetSection("Tink"));

builder.Services.AddHttpClient<ITinkBankingService, TinkBankingService>();

builder.Services.AddHttpClient<ICategoriaSeedService, CategoriaSeedService>();

builder.Services.AddScoped<ITransaccionesService, TransaccionesService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection("Jwt"));

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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                Console.WriteLine("JWT MESSAGE RECEIVED");
                Console.WriteLine($"Authorization header: {context.Request.Headers.Authorization}");
                Console.WriteLine($"Token leído por middleware: {context.Token}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("JWT TOKEN VALIDADO");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("JWT ERROR:");
                Console.WriteLine(context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine("JWT CHALLENGE:");
                Console.WriteLine(context.Error);
                Console.WriteLine(context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();

app.UseCors(corsPolicy);
// Configure the HTTP request pipeline.
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

app.UseAuthorization();
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

app.Use(async (context, next) =>
{
    Console.WriteLine($"REQUEST => {context.Request.Method} {context.Request.Path}{context.Request.QueryString}");
    await next();
    Console.WriteLine($"RESPONSE => {context.Response.StatusCode} {context.Request.Path}");
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
