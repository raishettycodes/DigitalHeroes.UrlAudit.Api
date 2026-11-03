using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Data;

public class PostgreSqlUrlAuditDbContext : UrlAuditDbContext
{
    public PostgreSqlUrlAuditDbContext(
        DbContextOptions<PostgreSqlUrlAuditDbContext> options)
        : base(options)
    {
    }
}