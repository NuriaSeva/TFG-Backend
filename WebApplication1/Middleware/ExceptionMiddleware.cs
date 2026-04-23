using System.Net;
using System.Text.Json;
using FinMind.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FinMind.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, errorCode) = ResolverError(ex);

            if ((int)statusCode >= 500)
            {
                _logger.LogError(
                    ex,
                    "Error no controlado ({ErrorCode}) en {Method} {Path}. TraceId={TraceId}",
                    errorCode,
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Error de negocio ({ErrorCode}) en {Method} {Path}. TraceId={TraceId}",
                    errorCode,
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);
            }

            await ManejarExcepcionAsync(context, ex);
        }
    }

    private static Task ManejarExcepcionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, errorCode) = ResolverError(ex);
        var esErrorInterno = statusCode == HttpStatusCode.InternalServerError;

        var respuesta = new
        {
            error = true,
            codigo = errorCode,
            status = (int)statusCode,
            mensaje = esErrorInterno
                ? "Se ha producido un error interno en el servidor."
                : ex.Message,
            detalle = esErrorInterno
                ? "Contacta con soporte indicando el traceId."
                : null,
            traceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(respuesta);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode statusCode, string errorCode) ResolverError(Exception ex)
    {
        return ex switch
        {
            NotFoundException => (HttpStatusCode.NotFound, "not_found"),
            BadRequestException => (HttpStatusCode.BadRequest, "bad_request"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "unauthorized"),
            ArgumentOutOfRangeException => (HttpStatusCode.BadRequest, "validation_error"),
            ArgumentException => (HttpStatusCode.BadRequest, "validation_error"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "invalid_operation"),
            DbUpdateException => (HttpStatusCode.Conflict, "db_conflict"),
            _ => (HttpStatusCode.InternalServerError, "internal_error")
        };
    }
}
