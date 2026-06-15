using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace KUKULCAN.Kernel.Database.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that converts
/// <c>EntityState.Deleted</c> into a soft-delete operation for entities
/// implementing <see cref="ISoftDeletable"/>.
/// </summary>
/// <remarks>
/// <para>
/// When EF Core detects a <c>Deleted</c> entry for a soft-deletable entity,
/// this interceptor:
/// <list type="number">
///   <item>Changes the state to <c>Modified</c>.</item>
///   <item>Calls <see cref="ISoftDeletable.MarkAsDeleted"/> to set
///         <c>IsDeleted=true</c>, <c>DeletedAt</c>, and <c>DeletedBy</c>.</item>
/// </list>
/// The record is <b>never physically removed</b> from the database.
/// </para>
/// <para>
/// To perform a physical delete (GDPR erasure, integration tests), use
/// <c>context.Database.ExecuteSqlRaw()</c> directly, bypassing the interceptor.
/// </para>
/// </remarks>
/// <remarks>Initialises the interceptor with the required services.</remarks>
public sealed class SoftDeleteInterceptor(ICurrentUser currentUser, IDateTimeProvider clock) : SaveChangesInterceptor
{

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ConvertDeletesToSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ConvertDeletesToSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ConvertDeletesToSoftDelete(DbContext? context)
    {
        if (context is null) return;

        DateTimeOffset now       = clock.UtcNow;
        string deletedBy = currentUser.IsAuthenticated ? currentUser.UserName : "system";

        List<EntityEntry<ISoftDeletable>> deletedEntries = context.ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        foreach (EntityEntry<ISoftDeletable> entry in deletedEntries)
        {
            entry.State = EntityState.Modified;   // prevent physical DELETE
            entry.Entity.MarkAsDeleted(deletedBy, now);
        }
    }
}
