using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace FinMind.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
      protected Guid ObtenerUsuarioId()
    {
        var claim =
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(claim))
        {
            throw new UnauthorizedAccessException("No se pudo resolver el usuario autenticado desde el token.");
        }

        return Guid.Parse(claim);
    }
}
