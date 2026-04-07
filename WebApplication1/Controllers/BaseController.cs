using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected Guid ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(claim))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        return Guid.Parse(claim);
    }
}