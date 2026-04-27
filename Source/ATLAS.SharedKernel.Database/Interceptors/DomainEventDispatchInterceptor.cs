namespace ATLAS.SharedKernel.Database.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that dispatches all pending
/// <see cref="IDomainEvent"/> instances collected by <see cref="IDomainEventHolder"/>
/// entities after each successful <c>SaveChangesAsync</c> call.
/// </summary>
/// <remarks>
/// <para>
/// Domain events are dispatched <b>after</b> the database commit to guarantee that
/// aggregate state is persisted before any handler reacts to it.
/// Events are dispatched in-process via MediatR's <see cref="IPublisher"/>.
/// </para>
/// <para>
/// Events are cleared from the aggregate before dispatching to prevent double-dispatch
/// if a handler triggers another <c>SaveChanges</c> within the same scope.
/// </para>
/// </remarks>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor
{
    private readonly IPublisher _publisher;

    /// <summary>Initialises the interceptor with the MediatR publisher.</summary>
    public DomainEventDispatchInterceptor(IPublisher publisher)
        => _publisher = publisher;

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int                           result,
        CancellationToken             cancellationToken = default)
    {
        await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int                           result)
    {
        DispatchDomainEventsAsync(eventData.Context, CancellationToken.None)
            .GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task DispatchDomainEventsAsync(
        DbContext?        context,
        CancellationToken cancellationToken)
    {
        if (context is null) return;

        var aggregates = context.ChangeTracker
            .Entries<IDomainEventHolder>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var events = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        // Clear before dispatching to prevent double-dispatch
        foreach (var aggregate in aggregates)
            aggregate.ClearDomainEvents();

        foreach (var domainEvent in events)
            await _publisher.Publish(domainEvent, cancellationToken);
    }
}
