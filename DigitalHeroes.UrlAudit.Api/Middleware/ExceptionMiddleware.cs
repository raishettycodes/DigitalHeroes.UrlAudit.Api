using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DigitalHeroes.UrlAudit.Api.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger)
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
                await HandleExceptionAsync(context, ex);
            }
        }

        //private async Task HandleExceptionAsync(
        //    HttpContext context,
        //    Exception exception)
        //{
        //    _logger.LogError(
        //        exception,
        //        "Unhandled Exception");

        //    context.Response.ContentType = "application/json";

        //    context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        //    var response = new
        //    {
        //        Success = false,
        //        Message = "An unexpected error occurred.",
        //        StatusCode = 500
        //    };

        //    var json = JsonSerializer.Serialize(response);

        //    await context.Response.WriteAsync(json);
        //}

        private async Task HandleExceptionAsync(
    HttpContext context,
    Exception exception)
        {
            _logger.LogError(exception, "Unhandled Exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var response = new
            {
                Success = false,
                Message = exception.Message,
                Exception = exception.GetType().Name,
                InnerException = exception.InnerException?.Message,
                StackTrace = exception.StackTrace
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}