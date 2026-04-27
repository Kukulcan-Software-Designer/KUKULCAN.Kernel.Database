namespace ATLAS.SharedKernel.Database.Interceptors;

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
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUser      _currentUser;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initialises the interceptor with the required services.</summary>
    public SoftDeleteInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    {
        _currentUser = currentUser;
        _clock       = clock;
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData      eventData,
        InterceptionResult<int> result)
    {
        ConvertDeletesToSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       cancellationToken = default)
    {
        ConvertDeletesToSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ConvertDeletesToSoftDelete(DbContext? context)
    {
        if (context is null) return;

        var now       = _clock.UtcNow;
        var deletedBy = _currentUser.IsAuthenticated ? _currentUser.UserName : "system";

        var deletedEntries = context.ChangeTracker
            .Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;   // prevent physical DELETE
            entry.Entity.MarkAsDeleted(deletedBy, now);
        }
    }
}
