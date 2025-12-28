using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DynamicDbApi.Infrastructure
{
    public class ValidationExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ValidationExceptionHandlerMiddleware> _logger;

        public ValidationExceptionHandlerMiddleware(RequestDelegate next, ILogger<ValidationExceptionHandlerMiddleware> logger)
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
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation failed for request: {RequestPath}, {RequestMethod}", context.Request.Path, context.Request.Method);
                _logger.LogWarning(ex, "Exception details: {Message}, {StackTrace}", ex.Message, ex.StackTrace);
                await HandleValidationExceptionAsync(context, ex);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument validation failed for request: {RequestPath}, {RequestMethod}", context.Request.Path, context.Request.Method);
                _logger.LogWarning(ex, "Exception details: {Message}, {ParamName}, {StackTrace}", ex.Message, ex.ParamName, ex.StackTrace);
                await HandleArgumentExceptionAsync(context, ex);
            }
            // 捕获所有其他异常，确保记录详细信息
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error for request: {RequestPath}, {RequestMethod}", context.Request.Path, context.Request.Method);
                _logger.LogError(ex, "Exception details: {Message}, {StackTrace}", ex.Message, ex.StackTrace);
                throw; // 重新抛出异常，让其他中间件处理
            }
        }

        private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            var errors = exception.Errors.Select(e => new
            {
                Field = e.PropertyName,
                Message = e.ErrorMessage,
                ErrorCode = e.ErrorCode
            });

            var response = new
            {
                success = false,
                message = "请求参数验证失败",
                errors = errors
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(json);
        }

        private static async Task HandleArgumentExceptionAsync(HttpContext context, ArgumentException exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            var response = new
            {
                success = false,
                message = exception.Message,
                parameter = exception.ParamName
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(json);
        }
    }

    public static class ValidationExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseValidationExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ValidationExceptionHandlerMiddleware>();
        }
    }
}