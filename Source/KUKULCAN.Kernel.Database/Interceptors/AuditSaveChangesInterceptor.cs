using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace KUKULCAN.Kernel.Database.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that automatically populates audit
/// fields (<c>CreatedAt</c>, <c>CreatedBy</c>, <c>UpdatedAt</c>, <c>UpdatedBy</c>)
/// on all entities implementing <see cref="IAuditable"/> before each save.
/// </summary>
/// <remarks>
/// <para>
/// Registered globally by <see cref="KukulcanDbContextBase"/>. No per-entity code required.
/// </para>
/// <para>
/// <c>CreatedBy</c> / <c>UpdatedBy</c> are populated from <see cref="ICurrentUser"/>.
/// When the user is not authenticated (e.g. background jobs or seeding) the value
/// falls back to <c>"system"</c>.
/// </para>
/// </remarks>
/// <remarks>Initializes the interceptor with the required services.</remarks>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, IDateTimeProvider clock) : SaveChangesInterceptor
{

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null) return;

        DateTimeOffset now  = clock.UtcNow;
        string? user = currentUser.IsAuthenticated ? currentUser.UserName : "system";

        foreach (EntityEntry<AuditableEntityBase<Guid>> entry in context.ChangeTracker.Entries<AuditableEntityBase<Guid>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreated(user, now);
                    break;

                case EntityState.Modified:
                    entry.Entity.SetUpdated(user, now);
                    break;
            }
        }
    }
}
