using System.Net;
using System.Text.Json;
using IPM.Application.DTOs.Common;
using Microsoft.EntityFrameworkCore;

namespace IPM.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Dữ liệu đã được thay đổi bởi người khác, vui lòng tải lại và thử lại"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Bạn không có quyền truy cập"),
            ArgumentException argEx => (HttpStatusCode.BadRequest, argEx.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Không tìm thấy dữ liệu"),
            _ => (HttpStatusCode.InternalServerError, "Đã xảy ra lỗi, vui lòng thử lại sau")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.FailResponse(message);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
