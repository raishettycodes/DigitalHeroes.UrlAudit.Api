using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DigitalHeroes.UrlAudit.Api.Data;

public class UrlAuditDbContextFactory
    : IDesignTimeDbContextFactory<UrlAuditDbContext>
{
    public UrlAuditDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "DigitalHeroes.UrlAudit.Api");

        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: false)
            .AddJsonFile(
                "appsettings.Development.json",
                optional: true,
                reloadOnChange: false)
            .AddUserSecrets<UrlAuditDbContextFactory>(
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection was not found. " +
                "Configure it using User Secrets or an environment variable.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<UrlAuditDbContext>();

        optionsBuilder.UseSqlServer(connectionString);

        return new UrlAuditDbContext(optionsBuilder.Options);
    }
}