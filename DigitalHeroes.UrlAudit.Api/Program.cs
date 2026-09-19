using Serilog;
using DigitalHeroes.UrlAudit.Api.Services;
using DigitalHeroes.UrlAudit.Api.Middleware;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using DigitalHeroes.UrlAudit.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using DigitalHeroes.UrlAudit.Api.Data;
using DigitalHeroes.UrlAudit.Api.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DigitalHeroes.UrlAudit.Api.Helpers;
using DigitalHeroes.UrlAudit.Api.Models;



Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Application Starting");


    var builder = WebApplication.CreateBuilder(args);
    

    builder.Services.Configure<RazorpayOptions>(
    builder.Configuration.GetSection("Razorpay"));
    builder.Services.AddScoped<RazorpayService>();

    // Add services to the container.
    builder.Host.UseSerilog();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAngular", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy
                    .WithOrigins(
                        "http://localhost:4200",
                        "https://localhost:4200"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
            else
            {
                policy
                    .WithOrigins(
                        "https://wonderful-sea-0b0123710.6.azurestaticapps.net"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        });
    });
    builder.Services.AddControllers();
    builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
    builder.Services.AddDbContext<UrlAuditDbContext>(options =>
    options.UseSqlServer(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    sqlServerOptions =>
    {
        sqlServerOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    }));
    builder.Services.Configure<AuditSettings>(
    builder.Configuration.GetSection("AuditSettings"));
    builder.Services.AddHealthChecks();
    builder.Services.AddMemoryCache();
    builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("FixedPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous"
                : httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

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
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "DigitalHeroes URL Audit API",
            Version = "v1"
        });

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: Bearer {token}",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    });


    // Register AuditService
    builder.Services.AddHttpClient<AuditService>();
    builder.Services.AddScoped<SeoAuditService>();
    builder.Services.AddScoped<IAuditHistoryService, AuditHistoryService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IWebsiteService, WebsiteService>();
    builder.Services.AddScoped<JwtTokenGenerator>();
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");

        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["Key"]!)),

            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddAuthorization();

    Console.WriteLine("===== BEFORE BUILD =====");
    var app = builder.Build();
    Console.WriteLine("===== AFTER BUILD =====");



    // Request ID Middleware (FIRST)
    app.UseMiddleware<RequestIdMiddleware>();

    // Exception Middleware
    app.UseMiddleware<ExceptionMiddleware>();

    // Serilog Request Logging
    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.MapGet("/", () => "DigitalHeroes URL Audit API is running successfully.");


    app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";

        await next();
    });
    app.UseCors("AllowAngular");
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");
    Console.WriteLine("===== BEFORE RUN =====");
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

