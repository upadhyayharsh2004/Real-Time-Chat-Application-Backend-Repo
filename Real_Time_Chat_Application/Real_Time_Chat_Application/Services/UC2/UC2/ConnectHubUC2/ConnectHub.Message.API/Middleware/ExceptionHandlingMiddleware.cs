using System.Net;
using System.Text.Json;
using ConnectHub.Message.Models.DTOs;

namespace ConnectHub.Message.Middleware;

/// <summary>
/// Global exception handler middleware — catches all unhandled exceptions
/// Returns consistent JSON error responses via ApiResponseDto
/// Same pattern as ConnectHub Auth service
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        var (statusCode, message) = ex switch
        {
            InvalidOperationException => (HttpStatusCode.Conflict, ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
            _ => (HttpStatusCode.InternalServerError, ex.ToString())
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponseDto<object>.Fail(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
