using Serilog.Context;

namespace DigitalHeroes.UrlAudit.Api.Middleware
{
    public class RequestIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestIdMiddleware> _logger;

        public RequestIdMiddleware(
            RequestDelegate next,
            ILogger<RequestIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = context.Request.Headers["X-Request-ID"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(requestId))
            {
                requestId = Guid.NewGuid().ToString();
            }

            _logger.LogInformation("Generated Request ID: {RequestId}", requestId);

            context.Response.Headers["X-Request-ID"] = requestId;

            using (LogContext.PushProperty("RequestId", requestId))
            {
                await _next(context);
            }
        }
    }
}