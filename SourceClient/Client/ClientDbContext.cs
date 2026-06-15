using KUKULCAN.Kernel.Abstractions.Interfaces.Infrastructure;
using KUKULCAN.Kernel.Database.Interceptors;
using KUKULCAN.Kernel.Database.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KUKULCAN.Kernel.Database.Client.Client;

/// <summary>
/// DbContext concreto de demostración.
/// Hereda de <see cref="KukulcanDbContextBase"/> para demostrar todos
/// sus interceptores y filtros globales.
/// </summary>
public sealed class ClientDbContext(IOptions<KukulcanDatabaseOptions> options, ITenantContext tenantContext, ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider, IPublisher publisher, SlowQueryInterceptor slowQueryInterceptor) :
    KukulcanDbContextBase(options, tenantContext, currentUser, dateTimeProvider, publisher)
{
    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<ClientProduct> Products => Set<ClientProduct>();
    public DbSet<DemoAuditLog> AuditLogs => Set<DemoAuditLog>();
    public DbSet<ClientOrder> Orders => Set<ClientOrder>();
    public DbSet<DemoTenantDocument> TenantDocuments => Set<DemoTenantDocument>();

    // ── OnConfiguring — añade SlowQueryInterceptor sobre los base ─────────────
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // SlowQueryInterceptor se añade aquí porque KukulcanDbContextBase lo registra
        // en DI como singleton pero NO lo añade en su propio OnConfiguring.
        optionsBuilder.AddInterceptors(slowQueryInterceptor);
    }

    // ── OnModelCreating ───────────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("demo");

        // Llama al base: ApplyConfigurationsFromAssembly + ApplySoftDeleteFilter
        //                + ApplyTenantFilter
        base.OnModelCreating(modelBuilder);

        // ── DemoProduct ───────────────────────────────────────────────────────
        modelBuilder.Entity<ClientProduct>(e =>
        {
            e.ToTable("Products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Category).HasMaxLength(100);
            e.Property(p => p.DeletedBy).HasMaxLength(256);
        });

        // ── DemoAuditLog ──────────────────────────────────────────────────────
        modelBuilder.Entity<DemoAuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(200).IsRequired();
            e.Property(a => a.PerformedBy).HasMaxLength(256).IsRequired();
            e.Property(a => a.Detail).HasMaxLength(1000);
        });

        // ── ClientOrder ─────────────────────────────────────────────────────────
        modelBuilder.Entity<ClientOrder>(e =>
        {
            e.ToTable("Orders");
            e.HasKey(o => o.Id);
            e.Property(o => o.OrderNumber).HasMaxLength(50).IsRequired();
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.Property(o => o.Status).HasMaxLength(50);
            // DomainEvents no se mapea a BD — es solo en memoria
            e.Ignore(o => o.DomainEvents);
        });

        // ── DemoTenantDocument ────────────────────────────────────────────────
        modelBuilder.Entity<DemoTenantDocument>(e =>
        {
            e.ToTable("TenantDocuments");
            e.HasKey(d => d.Id);
            e.Property(d => d.Title).HasMaxLength(300).IsRequired();
            e.Property(d => d.Content).HasMaxLength(4000);
        });
    }
}
