using KUKULCAN.Kernel.Abstractions.Interfaces.Domain;
using KUKULCAN.Kernel.Domain.Entities;
using MediatR;

namespace KUKULCAN.Kernel.Database.Client.Client;

// ══════════════════════════════════════════════════════════════════════════════
// DemoProduct — Demuestra: AuditSaveChangesInterceptor + SoftDeleteInterceptor
//               + filtro global soft-delete en ModelBuilder
// ══════════════════════════════════════════════════════════════════════════════
public sealed class ClientProduct : AuditableEntityBase<Guid>, ISoftDeletable
{
    public string  Name      { get; set; } = string.Empty;
    public decimal Price     { get; set; }
    public string  Category  { get; set; } = string.Empty;

    // ── EF Core constructor ───────────────────────────────────────────────────
    private ClientProduct() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    /// <summary>Crea un nuevo producto con Id generado internamente.</summary>
    public static ClientProduct Create(string name, decimal price, string category)
        => new() { Id = Guid.NewGuid(), Name = name, Price = price, Category = category };

    // ── ISoftDeletable ────────────────────────────────────────────────────────
    public bool             IsDeleted  { get; private set; }
    public DateTimeOffset?  DeletedAt  { get; private set; }
    public string?          DeletedBy  { get; private set; }

    public void MarkAsDeleted(string deletedBy, DateTimeOffset deletedAt)
    {
        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// DemoAuditLog — Demuestra: ImmutableEntityInterceptor
//                (cualquier intento de Modified/Deleted lanza excepción)
// ══════════════════════════════════════════════════════════════════════════════
public sealed class DemoAuditLog : IImmutable
{
    public Guid            Id          { get; init; } = Guid.NewGuid();
    public string          Action      { get; init; } = string.Empty;
    public string          PerformedBy { get; init; } = string.Empty;
    public string          Detail      { get; init; } = string.Empty;
    public DateTimeOffset  PerformedAt { get; init; } = DateTimeOffset.UtcNow;
}

// ══════════════════════════════════════════════════════════════════════════════
// ClientOrder + OrderPlacedEvent — Demuestra: DomainEventDispatchInterceptor
//                                (los eventos se despachan tras SaveChanges)
// ══════════════════════════════════════════════════════════════════════════════
public sealed class ClientOrder : AuditableEntityBase<Guid>, IDomainEventHolder
{
    private readonly List<IDomainEvent> _events = [];

    public string  OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string  Status      { get; set; } = "Pending";

    // ── EF Core constructor ───────────────────────────────────────────────────
    private ClientOrder() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    /// <summary>Crea una nueva orden con Id generado internamente.</summary>
    public static ClientOrder Create(string orderNumber, decimal totalAmount, string status = "Pending")
        => new() { Id = Guid.NewGuid(), OrderNumber = orderNumber, TotalAmount = totalAmount, Status = status };

    // ── IDomainEventHolder ────────────────────────────────────────────────────
    public IReadOnlyList<IDomainEvent> DomainEvents => _events.AsReadOnly();
    public void ClearDomainEvents() => _events.Clear();
    public void AddDomainEvent(IDomainEvent domainEvent) => _events.Add(domainEvent);
}

public sealed record OrderPlacedEvent(Guid OrderId, string OrderNumber, decimal TotalAmount) : IDomainEvent, INotification
{
    public Guid            EventId    { get; } = Guid.NewGuid();
    public DateTimeOffset  OccurredAt { get; } = DateTimeOffset.UtcNow;
}

// ══════════════════════════════════════════════════════════════════════════════
// DemoTenantDocument — Demuestra: filtro global de tenant en ModelBuilder
//                      (WHERE TenantId = @currentTenantId automático)
// ══════════════════════════════════════════════════════════════════════════════
public sealed class DemoTenantDocument : TenantEntityBase<Guid>
{
    public string Title   { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // ── EF Core constructor ───────────────────────────────────────────────────
    private DemoTenantDocument() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Crea un nuevo documento con Id generado internamente.
    /// TenantId es <c>protected init</c> en TenantEntityBase — solo accesible desde aquí.
    /// </summary>
    public static DemoTenantDocument Create(Guid tenantId, string title, string content)
        => new() { Id = Guid.NewGuid(), TenantId = tenantId, Title = title, Content = content };
}
