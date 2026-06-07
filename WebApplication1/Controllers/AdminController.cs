using System.Data;
using System.Security.Cryptography;
using System.Security.Claims;
using FinMind.Data;
using FinMind.DTO.Admin;
using FinMind.Models;
using FinMind.Models.Enitdades;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = RolesSistema.Admin)]
public class AdminController : ControllerBase
{
    private readonly FinMindDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminController> _logger;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AdminController(
        FinMindDbContext context,
        IWebHostEnvironment environment,
        ILogger<AdminController> logger,
        IPasswordHasher<Usuario> passwordHasher)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
        _passwordHasher = passwordHasher;
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> ObtenerUsuarios([FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 25)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 1, 100);

        var query = _context.Usuarios.AsNoTracking();

        var total = await query.CountAsync();

        var usuarios = await query
            .OrderByDescending(u => u.FechaCreacion)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(u => new AdminUsuarioResumenDto
            {
                Id = u.Id,
                Email = u.Email,
                Nombre = u.Nombre,
                Apellidos = u.Apellidos,
                Rol = u.Rol ?? RolesSistema.Usuario,
                Activo = u.Activo,
                FechaCreacion = u.FechaCreacion,
                FechaUltimoAcceso = u.FechaUltimoAcceso
            })
            .ToListAsync();

        return Ok(new AdminUsuariosPaginadosResponseDto
        {
            Usuarios = usuarios,
            Total = total,
            Pagina = pagina,
            TamanoPagina = tamanoPagina
        });
    }

    [HttpPatch("usuarios/{usuarioId:guid}/estado")]
    public async Task<IActionResult> ActualizarEstadoUsuario([FromRoute] Guid usuarioId, [FromBody] AdminActualizarEstadoUsuarioDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        var adminId = ObtenerUsuarioId();
        if (adminId == usuarioId && !dto.Activo)
        {
            return BadRequest(new { mensaje = "No puedes desactivar tu propio usuario administrador." });
        }

        usuario.Activo = dto.Activo;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = "Estado de usuario actualizado correctamente.",
            usuarioId = usuario.Id,
            activo = usuario.Activo
        });
    }

    [HttpPatch("usuarios/{usuarioId:guid}/rol")]
    public async Task<IActionResult> ActualizarRolUsuario([FromRoute] Guid usuarioId, [FromBody] AdminActualizarRolUsuarioDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Rol))
        {
            return BadRequest(new { mensaje = "Debes indicar un rol." });
        }

        var rolNormalizado = dto.Rol.Trim();
        if (!string.Equals(rolNormalizado, RolesSistema.Usuario, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(rolNormalizado, RolesSistema.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensaje = "Rol inválido. Valores permitidos: User o Admin." });
        }

        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        var nuevoRol = string.Equals(rolNormalizado, RolesSistema.Admin, StringComparison.OrdinalIgnoreCase)
            ? RolesSistema.Admin
            : RolesSistema.Usuario;

        var adminId = ObtenerUsuarioId();
        if (adminId == usuarioId && nuevoRol != RolesSistema.Admin)
        {
            return BadRequest(new { mensaje = "No puedes quitarte el rol Admin a ti mismo." });
        }

        usuario.Rol = nuevoRol;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = "Rol actualizado correctamente.",
            usuarioId = usuario.Id,
            rol = usuario.Rol
        });
    }

    [HttpPost("usuarios/{usuarioId:guid}/reset-password")]
    public async Task<IActionResult> ResetearPasswordUsuario([FromRoute] Guid usuarioId)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario is null)
        {
            return NotFound(new { mensaje = "Usuario no encontrado." });
        }

        var passwordTemporal = GenerarPasswordTemporal();
        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, passwordTemporal);
        usuario.FechaCambioPassword = DateTime.UtcNow;
        usuario.DebeCambiarPassword = true;

        await _context.SaveChangesAsync();

        return Ok(new AdminResetPasswordResponseDto
        {
            UsuarioId = usuario.Id,
            PasswordTemporal = passwordTemporal,
            FechaGeneracionUtc = DateTime.UtcNow
        });
    }

    [HttpGet("health")]
    public async Task<IActionResult> ObtenerHealthAdmin()
    {
        var response = new AdminHealthResponseDto
        {
            TimestampUtc = DateTime.UtcNow,
            Entorno = _environment.EnvironmentName,
            BaseDeDatos = new AdminDatabaseStatusDto
            {
                Proveedor = _context.Database.ProviderName ?? "desconocido"
            }
        };

        try
        {
            response.BaseDeDatos.Conectada = await _context.Database.CanConnectAsync();

            if (response.BaseDeDatos.Conectada)
            {
                response.BaseDeDatos.TotalUsuarios = await _context.Usuarios.LongCountAsync();
                response.BaseDeDatos.TamanoBytes = await ObtenerTamanoBaseDatosAsync(response.BaseDeDatos.Proveedor);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo recuperar el estado completo de la base de datos.");
        }

        response.Almacenamiento = ObtenerEstadoDisco();

        return Ok(response);
    }

    private async Task<long?> ObtenerTamanoBaseDatosAsync(string provider)
    {
        if (!provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COALESCE(SUM(data_length + index_length), 0)
            FROM information_schema.tables
            WHERE table_schema = DATABASE();";

        var result = await command.ExecuteScalarAsync();
        if (result is null || result is DBNull)
        {
            return null;
        }

        return Convert.ToInt64(result);
    }

    private static AdminStorageStatusDto ObtenerEstadoDisco()
    {
        var basePath = AppContext.BaseDirectory;
        var rootPath = Path.GetPathRoot(basePath) ?? basePath;

        var drive = new DriveInfo(rootPath);
        var total = drive.TotalSize;
        var libre = drive.AvailableFreeSpace;

        return new AdminStorageStatusDto
        {
            Unidad = drive.Name,
            TotalBytes = total,
            DisponibleBytes = libre,
            PorcentajeLibre = total == 0 ? 0 : Math.Round((decimal)libre / total * 100, 2)
        };
    }

    private static string GenerarPasswordTemporal(int longitud = 12)
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string numeros = "23456789";
        const string simbolos = "!@#$%&*";
        var grupos = new[] { mayusculas, minusculas, numeros, simbolos };
        var todos = string.Concat(grupos);

        var resultado = new List<char>
        {
            mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)],
            minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
            simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)]
        };

        while (resultado.Count < longitud)
        {
            resultado.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);
        }

        for (var i = resultado.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (resultado[i], resultado[j]) = (resultado[j], resultado[i]);
        }

        return new string(resultado.ToArray());
    }

    private Guid ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var usuarioId))
        {
            throw new UnauthorizedAccessException("No se ha podido identificar al usuario autenticado.");
        }

        return usuarioId;
    }

}
