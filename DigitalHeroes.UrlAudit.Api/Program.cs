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
                    "https://localhost:4200",
                    "https://wonderful-sea-0b0123710.6.azurestaticapps.net"
                )
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    builder.Services.AddControllers();
    builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
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

        options.RequireHttpsMetadata = false;
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

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("===== AUTH FAILED =====");
                Console.WriteLine(context.Exception.Message);
                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                Console.WriteLine("===== TOKEN VALID =====");
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                Console.WriteLine("===== CHALLENGE =====");
                Console.WriteLine(context.Error);
                Console.WriteLine(context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

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
    app.UseAuthentication();
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

