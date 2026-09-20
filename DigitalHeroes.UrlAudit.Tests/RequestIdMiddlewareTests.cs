using DigitalHeroes.UrlAudit.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DigitalHeroes.UrlAudit.Tests;

public class RequestIdMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenRequestIdExists_PreservesRequestId()
    {
        const string requestId = "existing-request-id";
        var nextCalled = false;

        RequestDelegate next = context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = requestId;

        await middleware.Invoke(context);

        Assert.True(nextCalled);
        Assert.Equal(requestId, context.Response.Headers["X-Request-ID"].ToString());
    }

    [Fact]
    public async Task Invoke_WhenRequestIdMissing_GeneratesRequestId()
    {
        RequestDelegate next = _ => Task.CompletedTask;

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();

        await middleware.Invoke(context);

        var responseRequestId = context.Response.Headers["X-Request-ID"].ToString();

        Assert.False(string.IsNullOrWhiteSpace(responseRequestId));
        Assert.True(Guid.TryParse(responseRequestId, out _));
    }

    [Fact]
    public async Task Invoke_WhenRequestIdIsWhitespace_GeneratesRequestId()
    {
        RequestDelegate next = _ => Task.CompletedTask;

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = "   ";

        await middleware.Invoke(context);

        var responseRequestId = context.Response.Headers["X-Request-ID"].ToString();

        Assert.False(string.IsNullOrWhiteSpace(responseRequestId));
        Assert.True(Guid.TryParse(responseRequestId, out _));
    }

    [Fact]
    public async Task Invoke_SetsResponseRequestId_BeforeCallingNext()
    {
        const string requestId = "request-id-123";
        string? requestIdSeenByNext = null;

        RequestDelegate next = context =>
        {
            requestIdSeenByNext =
                context.Response.Headers["X-Request-ID"].ToString();

            return Task.CompletedTask;
        };

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = requestId;

        await middleware.Invoke(context);

        Assert.Equal(requestId, requestIdSeenByNext);
    }

    [Fact]
    public async Task Invoke_CallsNextMiddleware()
    {
        var nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();

        await middleware.Invoke(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Invoke_LogsRequestId()
    {
        const string requestId = "logged-request-id";

        RequestDelegate next = _ => Task.CompletedTask;

        var logger = new Mock<ILogger<RequestIdMiddleware>>();
        var middleware = new RequestIdMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = requestId;

        await middleware.Invoke(context);

        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Generated Request ID")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
