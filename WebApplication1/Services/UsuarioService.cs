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

        var nombreNormalizado = dto.Nombre.Trim();

        var apellidosNormalizados = string.IsNullOrWhiteSpace(dto.Apellidos)
            ? null
            : dto.Apellidos.Trim();
        var monedaNormalizada = dto.MonedaPreferida.Trim().ToUpperInvariant();
        var idiomaNormalizado = dto.Idioma.Trim().ToLowerInvariant();

        usuario.Nombre = nombreNormalizado;
        usuario.Apellidos = apellidosNormalizados;
        usuario.MonedaPreferida = monedaNormalizada;
        usuario.Idioma = idiomaNormalizado;

        await _context.SaveChangesAsync();

        return MapearPerfil(usuario);
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
