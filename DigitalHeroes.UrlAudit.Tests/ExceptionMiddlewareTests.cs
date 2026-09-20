using System.Text.Json;
using DigitalHeroes.UrlAudit.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DigitalHeroes.UrlAudit.Tests;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextCompletes_DoesNotModifyResponse()
    {
        var nextCalled = false;

        RequestDelegate next = context =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ReturnsInternalServerError()
    {
        var exception = new InvalidOperationException("Test exception");

        RequestDelegate next = _ => throw exception;

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_SetsJsonContentType()
    {
        RequestDelegate next = _ =>
            throw new InvalidOperationException("Test exception");

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.NotNull(context.Response.ContentType);
        Assert.StartsWith(
            "application/json",
            context.Response.ContentType,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ReturnsExceptionDetails()
    {
        var exception = new InvalidOperationException("Something went wrong");

        RequestDelegate next = _ => throw exception;

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.False(string.IsNullOrWhiteSpace(json));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Something went wrong",
            root.GetProperty("message").GetString());

        Assert.Equal(
            nameof(InvalidOperationException),
            root.GetProperty("exception").GetString());

        Assert.True(root.TryGetProperty("stackTrace", out var stackTrace));
        Assert.False(string.IsNullOrWhiteSpace(stackTrace.GetString()));
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsWithInnerException_ReturnsInnerExceptionMessage()
    {
        var innerException = new ArgumentException("Inner failure");

        var exception = new InvalidOperationException(
            "Outer failure",
            innerException);

        RequestDelegate next = _ => throw exception;

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        Assert.False(string.IsNullOrWhiteSpace(json));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(
            "Inner failure",
            root.GetProperty("innerException").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_LogsException()
    {
        var exception = new InvalidOperationException("Logging test");

        RequestDelegate next = _ => throw exception;

        var logger = new Mock<ILogger<ExceptionMiddleware>>();
        var middleware = new ExceptionMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Unhandled Exception")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
