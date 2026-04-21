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
            throw new UnauthorizedAccessException("No hemos podido validar tu sesión. Vuelve a iniciar sesión.");
        }

        return Guid.Parse(claim);
    }
}
