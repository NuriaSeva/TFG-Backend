using FinMind.Data;
using FinMind.DTO.Autenticacion;
using FinMind.Models;
using FinMind.Models.Enitdades;
using FinMind.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FinMind.Services;

public class UsuarioService : IUsuarioService
{
    private readonly FinMindDbContext _context;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly JwtOptions _jwtOptions;

    public UsuarioService(
        FinMindDbContext context,
        IPasswordHasher<Usuario> passwordHasher,
        IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AutenticacionResponseDto> RegistrarAsync(RegistroUsuarioDto dto)
    {
        ValidarPasswordSegura(dto.Password);

        var emailNormalizado = dto.Email.Trim().ToLowerInvariant();

        var existeUsuario = await _context.Usuarios
            .AnyAsync(u => u.Email == emailNormalizado);

        if (existeUsuario)
        {
            throw new InvalidOperationException("Ya existe un usuario registrado con ese correo electrónico.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Email = emailNormalizado,
            Nombre = dto.Nombre.Trim(),
            Apellidos = string.IsNullOrWhiteSpace(dto.Apellidos) ? null : dto.Apellidos.Trim(),
            MonedaPreferida = "EUR",
            Idioma = "es",
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            FechaCambioPassword = DateTime.UtcNow
        };

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, dto.Password);

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return GenerarRespuestaAutenticacion(usuario);
    }

    public async Task<AutenticacionResponseDto> IniciarSesionAsync(InicioSesionDto dto)
    {
        var emailNormalizado = dto.Email.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado);

        if (usuario is null)
        {
            throw new UnauthorizedAccessException("Correo o contraseña incorrectos.");
        }

        if (!usuario.Activo)
        {
            throw new UnauthorizedAccessException("La cuenta está desactivada.");
        }

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.Password);

        if (resultado == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Correo o contraseña incorrectos.");
        }

        usuario.FechaUltimoAcceso = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return GenerarRespuestaAutenticacion(usuario);
    }

    public async Task CambiarPasswordAsync(Guid usuarioId, CambiarPasswordDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario is null)
        {
            throw new UnauthorizedAccessException("No se ha encontrado el usuario autenticado.");
        }

        if (string.IsNullOrWhiteSpace(dto.PasswordActual))
        {
            throw new InvalidOperationException("Debes indicar la contraseña actual.");
        }

        if (string.IsNullOrWhiteSpace(dto.PasswordNueva))
        {
            throw new InvalidOperationException("Debes indicar la nueva contraseña.");
        }

        var resultadoPasswordActual = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.PasswordActual);

        if (resultadoPasswordActual == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("La contraseña actual no es correcta.");
        }

        var nuevaEsIgualActual = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, dto.PasswordNueva);
        if (nuevaEsIgualActual != PasswordVerificationResult.Failed)
        {
            throw new InvalidOperationException("La nueva contraseña no puede ser igual a la actual.");
        }

        ValidarPasswordSegura(dto.PasswordNueva);

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, dto.PasswordNueva);
        usuario.FechaCambioPassword = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<PerfilUsuarioResponseDto> ObtenerPerfilAsync(Guid usuarioId)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario is null)
        {
            throw new UnauthorizedAccessException("No se ha encontrado el usuario autenticado.");
        }

        return MapearPerfil(usuario);
    }

    public async Task<PerfilUsuarioResponseDto> ActualizarPerfilAsync(Guid usuarioId, ActualizarPerfilUsuarioDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);

        if (usuario is null)
        {
            throw new UnauthorizedAccessException("No se ha encontrado el usuario autenticado.");
        }

        var nombreNormalizado = (dto.Nombre ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nombreNormalizado))
        {
            throw new InvalidOperationException("El nombre es obligatorio.");
        }

        if (nombreNormalizado.Length < 2)
        {
            throw new InvalidOperationException("El nombre debe tener al menos 2 caracteres.");
        }

        if (nombreNormalizado.Length > 80)
        {
            throw new InvalidOperationException("El nombre no puede superar los 80 caracteres.");
        }

        var apellidosNormalizados = string.IsNullOrWhiteSpace(dto.Apellidos)
            ? null
            : dto.Apellidos.Trim();

        if (!string.IsNullOrEmpty(apellidosNormalizados) && apellidosNormalizados.Length > 120)
        {
            throw new InvalidOperationException("Los apellidos no pueden superar los 120 caracteres.");
        }

        var monedaNormalizada = (dto.MonedaPreferida ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(monedaNormalizada) || monedaNormalizada.Length != 3)
        {
            throw new InvalidOperationException("La moneda preferida debe ser un código de 3 letras.");
        }

        var idiomaNormalizado = (dto.Idioma ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(idiomaNormalizado) || idiomaNormalizado.Length < 2 || idiomaNormalizado.Length > 5)
        {
            throw new InvalidOperationException("El idioma debe tener entre 2 y 5 caracteres.");
        }

        usuario.Nombre = nombreNormalizado;
        usuario.Apellidos = apellidosNormalizados;
        usuario.MonedaPreferida = monedaNormalizada;
        usuario.Idioma = idiomaNormalizado;

        await _context.SaveChangesAsync();

        return MapearPerfil(usuario);
    }

    private static void ValidarPasswordSegura(string password)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errores.Add("La contraseña es obligatoria.");
        }
        else
        {
            if (password.Length < 8)
                errores.Add("Debe tener al menos 8 caracteres.");

            if (!password.Any(char.IsUpper))
                errores.Add("Debe incluir al menos una letra mayúscula.");

            if (!password.Any(char.IsLower))
                errores.Add("Debe incluir al menos una letra minúscula.");

            if (!password.Any(char.IsDigit))
                errores.Add("Debe incluir al menos un número.");

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                errores.Add("Debe incluir al menos un carácter especial.");
        }

        if (errores.Count > 0)
        {
            throw new InvalidOperationException("La contraseña no cumple los requisitos de seguridad: " + string.Join(" ", errores));
        }
    }

    private AutenticacionResponseDto GenerarRespuestaAutenticacion(Usuario usuario)
    {
        var expiracion = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);

        if (string.IsNullOrWhiteSpace(_jwtOptions.Key))
        {
            throw new InvalidOperationException("La clave JWT no está configurada.");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Issuer))
        {
            throw new InvalidOperationException("El issuer JWT no está configurado.");
        }

        if (string.IsNullOrWhiteSpace(_jwtOptions.Audience))
        {
            throw new InvalidOperationException("La audience JWT no está configurada.");
        }

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim("email", usuario.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiracion,
            signingCredentials: credentials
        );

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        return new AutenticacionResponseDto
        {
            UsuarioId = usuario.Id,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Token = token,
            ExpiracionToken = expiracion
        };
    }

    private static PerfilUsuarioResponseDto MapearPerfil(Usuario usuario)
    {
        return new PerfilUsuarioResponseDto
        {
            Email = usuario.Email,
            Nombre = usuario.Nombre,
            Apellidos = usuario.Apellidos,
            MonedaPreferida = usuario.MonedaPreferida,
            Idioma = usuario.Idioma
        };
    }
}
