using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Data;

public class UrlAuditDbContext : DbContext
{
    public UrlAuditDbContext(DbContextOptions<UrlAuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditHistory> AuditHistories => Set<AuditHistory>();
}