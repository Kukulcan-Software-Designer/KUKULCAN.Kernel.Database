namespace KUKULCAN.Kernel.Database.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that enforces immutability for
/// entities implementing <see cref="IImmutable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Entities marked with <see cref="IImmutable"/> (audit log entries, persisted
/// domain events, financial journal lines, digital signatures) must never be
/// updated or deleted after insertion.
/// </para>
/// <para>
/// Throws <see cref="InvalidOperationException"/> if EF Core's change tracker
/// detects a <c>Modified</c> or <c>Deleted</c> state for an immutable entity,
/// preventing silent data corruption.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // This will throw InvalidOperationException:
/// auditLogEntry.Action = "tampered";
/// await unitOfWork.SaveChangesAsync(ct);  // ← interceptor blocks this
/// </code>
/// </example>
public sealed class ImmutableEntityInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ThrowIfImmutableEntityModified(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ThrowIfImmutableEntityModified(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void ThrowIfImmutableEntityModified(DbContext? context)
    {
        if (context is null) return;

        var violations = context.ChangeTracker
            .Entries<IImmutable>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.GetType().Name)
            .ToList();

        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"Attempt to modify or delete immutable entity/entities: " +
                $"{string.Join(", ", violations)}. " +
                $"Entities implementing IImmutable are append-only and " +
                $"cannot be updated or deleted after insertion.");
    }
}
