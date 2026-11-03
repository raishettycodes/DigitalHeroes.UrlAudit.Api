
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DigitalHeroes.UrlAudit.Api.Data;

public class PostgreSqlUrlAuditDbContextFactory
    : IDesignTimeDbContextFactory<PostgreSqlUrlAuditDbContext>
{
    public PostgreSqlUrlAuditDbContext CreateDbContext(string[] args)
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
            .AddUserSecrets<PostgreSqlUrlAuditDbContextFactory>(
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("PostgreSqlConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSqlConnection was not found. " +
                "Configure it using User Secrets or an environment variable.");
        }

        var optionsBuilder =
            new DbContextOptionsBuilder<PostgreSqlUrlAuditDbContext>();

        optionsBuilder.UseNpgsql(
    connectionString,
    npgsqlOptions =>
        npgsqlOptions.MigrationsHistoryTable(
            "__EFMigrationsHistory_PostgreSQL"));

        return new PostgreSqlUrlAuditDbContext(optionsBuilder.Options);
    }
}
