using DigitalHeroes.UrlAudit.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalHeroes.UrlAudit.Api.Data;

public class UrlAuditDbContext : DbContext
{
    public UrlAuditDbContext(
        DbContextOptions<UrlAuditDbContext> options)
        : base(options)
    {
    }

    // =========================================================
    // USERS
    // =========================================================

    public DbSet<User> Users => Set<User>();

    public DbSet<PasswordResetToken> PasswordResetTokens =>
    Set<PasswordResetToken>();


    // =========================================================
    // WEBSITES
    // =========================================================

    public DbSet<Website> Websites => Set<Website>();


    // =========================================================
    // AUDIT
    // =========================================================

    public DbSet<AuditHistory> AuditHistories =>
        Set<AuditHistory>();

    public DbSet<AuditResult> AuditResults =>
        Set<AuditResult>();


    // =========================================================
    // SUBSCRIPTION
    // =========================================================

    public DbSet<Subscription> Subscriptions =>
        Set<Subscription>();

    public DbSet<Payment> Payments =>
        Set<Payment>();

    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents =>
        Set<PaymentWebhookEvent>();


    // =========================================================
    // NOTIFICATIONS
    // =========================================================

    public DbSet<Notification> Notifications =>
        Set<Notification>();


    // =========================================================
    // MODEL CONFIGURATION
    // =========================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        // -----------------------------------------------------
        // USER → WEBSITE
        // -----------------------------------------------------

        modelBuilder.Entity<Website>()
            .HasOne(w => w.User)
            .WithMany(u => u.Websites)
            .HasForeignKey(w => w.UserId);


        // -----------------------------------------------------
        // WEBSITE → AUDIT HISTORY
        // -----------------------------------------------------

        modelBuilder.Entity<AuditHistory>()
            .HasOne(a => a.Website)
            .WithMany(w => w.AuditHistories)
            .HasForeignKey(a => a.WebsiteId)
            .OnDelete(DeleteBehavior.Cascade);


        // -----------------------------------------------------
        // USER → SUBSCRIPTION
        // -----------------------------------------------------

        modelBuilder.Entity<Subscription>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        // -----------------------------------------------------
        // ONE ACTIVE SUBSCRIPTION RECORD PER USER
        // -----------------------------------------------------

        modelBuilder.Entity<Subscription>()
            .HasIndex(s => s.UserId)
            .IsUnique();


        // -----------------------------------------------------
        // SUBSCRIPTION PRICE
        // -----------------------------------------------------

        modelBuilder.Entity<Subscription>()
            .Property(s => s.MonthlyPrice)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);


        // -----------------------------------------------------
        // SUBSCRIPTION DEFAULTS
        // -----------------------------------------------------

        modelBuilder.Entity<Subscription>()
            .Property(s => s.Plan)
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<Subscription>()
            .Property(s => s.Status)
            .HasMaxLength(30)
            .IsRequired();

        modelBuilder.Entity<Subscription>()
            .Property(s => s.PaymentProvider)
            .HasMaxLength(50);

        modelBuilder.Entity<Subscription>()
            .Property(s => s.ExternalSubscriptionId)
            .HasMaxLength(200);
         
        modelBuilder.Entity<PaymentWebhookEvent>()
             .HasIndex(e => e.EventId)
             .IsUnique();

        // -----------------------------------------------------
        // PASSWORD RESET TOKENS
        // -----------------------------------------------------

        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .Property(t => t.TokenHash)
            .HasMaxLength(128);

    }
}