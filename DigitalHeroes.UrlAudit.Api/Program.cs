using Serilog;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Api.Middleware;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DigitalHeroes.UrlAudit.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using DigitalHeroes.UrlAudit.Api.Data;


Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Application Starting");


    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Host.UseSerilog();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular", policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:4200",
                    "https://localhost:4200"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    builder.Services.AddControllers();
    builder.Services.AddDbContext<UrlAuditDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.Configure<AuditSettings>(
    builder.Configuration.GetSection("AuditSettings"));
    builder.Services.AddHealthChecks();
    builder.Services.AddMemoryCache();
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("FixedPolicy", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                Success = false,
                StatusCode = 429,
                Message = "Too many requests. Please try again after one minute."
            }, token);
        };
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        var xmlFilename =
            $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";

        options.IncludeXmlComments(
            Path.Combine(AppContext.BaseDirectory, xmlFilename));
    });


    // Register AuditService
    builder.Services.AddHttpClient<AuditService>();

var app = builder.Build();

   

    // Request ID Middleware (FIRST)
    app.UseMiddleware<RequestIdMiddleware>();

    // Exception Middleware
    app.UseMiddleware<ExceptionMiddleware>();

    // Serilog Request Logging
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => "DigitalHeroes URL Audit API is running successfully.");


    app.UseRateLimiter();
    app.UseHttpsRedirection();
    app.UseCors("AllowAngular");
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

