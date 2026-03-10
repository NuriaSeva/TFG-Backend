using FindMind.Data;
using FindMind.Middleware;
using Microsoft.EntityFrameworkCore;
using FindMind.Models;
using FindMind.Services;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? throw new InvalidOperationException("No se encontró la cadena de conexión");

builder.Services.AddDbContext<FindMindDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<TrueLayerOptions>(
    builder.Configuration.GetSection("TrueLayer"));

builder.Services.AddHttpClient<TrueLayerService>();
var app = builder.Build();

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
