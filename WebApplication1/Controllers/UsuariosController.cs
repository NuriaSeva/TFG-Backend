using FindMind.Data;
using FindMind.Common.Exceptions;
using FindMind.Models.Enitdades;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindMind.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly FindMindDbContext _context;

    public UsuariosController(FindMindDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Usuario>>> ObtenerTodos()
    {
        var usuarios = await _context.Usuarios.ToListAsync();
        return Ok(usuarios);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Usuario>> ObtenerPorId(Guid id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
            throw new NotFoundException("No se ha encontrado el usuario.");

        return Ok(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<Usuario>> Crear(Usuario usuario)
    {
        var emailExiste = await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email);
        if (emailExiste)
            throw new BadRequestException("Ya existe un usuario con ese email.");

        usuario.Id = Guid.NewGuid();
        usuario.FechaCreacion = DateTime.UtcNow;

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(ObtenerPorId), new { id = usuario.Id }, usuario);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(Guid id, Usuario usuario)
    {
        if (id != usuario.Id)
            throw new BadRequestException("El id de la URL no coincide con el del cuerpo.");

        var usuarioActual = await _context.Usuarios.FindAsync(id);
        if (usuarioActual == null)
            throw new NotFoundException("No se ha encontrado el usuario.");

        var emailDuplicado = await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email && u.Id != id);
        if (emailDuplicado)
            throw new BadRequestException("Ya existe otro usuario con ese email.");

        usuarioActual.Email = usuario.Email;
        usuarioActual.PasswordHash = usuario.PasswordHash;
        usuarioActual.Nombre = usuario.Nombre;
        usuarioActual.Apellidos = usuario.Apellidos;
        usuarioActual.MonedaPreferida = usuario.MonedaPreferida;
        usuarioActual.Idioma = usuario.Idioma;
        usuarioActual.Activo = usuario.Activo;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);

        if (usuario == null)
            throw new NotFoundException("No se ha encontrado el usuario.");

        _context.Usuarios.Remove(usuario);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}