using FindMind.Data;
using FindMind.Interfaces;
using FindMind.Middleware;
using FindMind.Models;
using FindMind.Services;
using Microsoft.EntityFrameworkCore;


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


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new InvalidOperationException("No se encontró la cadena de conexión");

builder.Services.AddDbContext<FindMindDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<TinkOptions>(
    builder.Configuration.GetSection("Tink"));

builder.Services.AddHttpClient<ITinkBankingService, TinkBankingService>();

builder.Services.AddHttpClient<ICategoriaSeedService, CategoriaSeedService>();

var app = builder.Build();

app.UseCors(corsPolicy);
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
