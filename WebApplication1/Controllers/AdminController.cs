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
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;

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
    private readonly IAOptions _iaOptions;

    public AdminController(
        FinMindDbContext context,
        IWebHostEnvironment environment,
        ILogger<AdminController> logger,
        IPasswordHasher<Usuario> passwordHasher,
        IOptions<IAOptions> iaOptions)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
        _passwordHasher = passwordHasher;
        _iaOptions = iaOptions.Value;
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
        response.ModeloPrediccionGasto = ObtenerEstadoModeloPrediccionGasto();

        return Ok(response);
    }

    private AdminModeloPrediccionGastoDto ObtenerEstadoModeloPrediccionGasto()
    {
        var datasetPath = ResolverRuta(_iaOptions.PrediccionGastoDatasetPath);
        var modelPath = ResolverRuta(Path.Combine(_iaOptions.ModelOutputPath, _iaOptions.PrediccionGastoModelFileName));

        var response = new AdminModeloPrediccionGastoDto
        {
            DatasetDisponible = System.IO.File.Exists(datasetPath),
            ModeloDisponible = System.IO.File.Exists(modelPath),
            FechaModeloUtc = System.IO.File.Exists(modelPath)
                ? System.IO.File.GetLastWriteTimeUtc(modelPath)
                : null
        };

        if (!response.DatasetDisponible)
        {
            response.Mensaje = "Dataset de prediccion no disponible.";
            return response;
        }

        try
        {
            var mlContext = new MLContext(seed: 12);
            var data = mlContext.Data.LoadFromTextFile<PrediccionGastoTrainingInput>(
                path: datasetPath,
                hasHeader: true,
                separatorChar: ';',
                allowQuoting: true,
                trimWhitespace: true);

            response.RegistrosDataset = mlContext.Data
                .CreateEnumerable<PrediccionGastoTrainingInput>(data, reuseRowObject: false)
                .Count();

            if (response.RegistrosDataset < 20)
            {
                response.Mensaje = "Dataset insuficiente para evaluacion.";
                return response;
            }

            var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2, seed: 12);
            var pipeline = mlContext.Transforms.Concatenate(
                    "Features",
                    nameof(PrediccionGastoTrainingInput.DiaMes),
                    nameof(PrediccionGastoTrainingInput.DiasMes),
                    nameof(PrediccionGastoTrainingInput.PorcentajeMesTranscurrido),
                    nameof(PrediccionGastoTrainingInput.GastoAcumulado),
                    nameof(PrediccionGastoTrainingInput.IngresosMes),
                    nameof(PrediccionGastoTrainingInput.MediaGasto3Meses),
                    nameof(PrediccionGastoTrainingInput.GastoMedioDiarioActual),
                    nameof(PrediccionGastoTrainingInput.Mes))
                .Append(mlContext.Regression.Trainers.Sdca(
                    labelColumnName: nameof(PrediccionGastoTrainingInput.GastoFinalMes),
                    featureColumnName: "Features"));

            var model = pipeline.Fit(split.TrainSet);
            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.Regression.Evaluate(
                predictions,
                labelColumnName: nameof(PrediccionGastoTrainingInput.GastoFinalMes));

            response.Mae = Math.Round((decimal)metrics.MeanAbsoluteError, 2);
            response.Rmse = Math.Round((decimal)metrics.RootMeanSquaredError, 2);
            response.R2 = Math.Round((decimal)metrics.RSquared, 4);
            response.Mensaje = "Metricas calculadas sobre particion de test del dataset.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron calcular las metricas del modelo de prediccion de gasto.");
            response.Mensaje = "No se pudieron calcular las metricas del modelo.";
        }

        return response;
    }

    private string ResolverRuta(string relativeOrAbsolute)
    {
        if (Path.IsPathRooted(relativeOrAbsolute))
            return relativeOrAbsolute;

        return Path.Combine(_environment.ContentRootPath, relativeOrAbsolute);
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

    private sealed class PrediccionGastoTrainingInput
    {
        [LoadColumn(0)]
        public float DiaMes { get; set; }

        [LoadColumn(1)]
        public float DiasMes { get; set; }

        [LoadColumn(2)]
        public float PorcentajeMesTranscurrido { get; set; }

        [LoadColumn(3)]
        public float GastoAcumulado { get; set; }

        [LoadColumn(4)]
        public float IngresosMes { get; set; }

        [LoadColumn(5)]
        public float MediaGasto3Meses { get; set; }

        [LoadColumn(6)]
        public float GastoMedioDiarioActual { get; set; }

        [LoadColumn(7)]
        public float Mes { get; set; }

        [LoadColumn(8)]
        public float GastoFinalMes { get; set; }
    }
}
