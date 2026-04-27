namespace ATLAS.SharedKernel.Database.Extensions;

/// <summary>
/// Extension methods for <see cref="ModelBuilder"/> that apply ATLAS global
/// query filters and conventions during <c>OnModelCreating</c>.
/// Called automatically by <see cref="AtlasDbContextBase"/>.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the global soft-delete filter to all entities implementing
    /// <see cref="ISoftDeletable"/>, transparently excluding deleted records
    /// from all normal queries.
    /// </summary>
    /// <remarks>
    /// To query deleted records explicitly, use
    /// <c>dbContext.Set&lt;T&gt;().IgnoreQueryFilters()</c>.
    /// </remarks>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <example>
    /// <code>
    /// // In AtlasDbContextBase.OnModelCreating:
    /// modelBuilder.ApplySoftDeleteFilter();
    ///
    /// // Effect: every query against ISoftDeletable entities will have
    /// // WHERE IsDeleted = false added automatically by EF Core.
    /// </code>
    /// </example>
    public static ModelBuilder ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)) continue;

            typeof(ModelBuilderExtensions)
                .GetMethod(nameof(SetSoftDeleteFilter),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(null, [modelBuilder]);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Applies the global tenant isolation filter to all entities implementing
    /// <see cref="ITenantAware"/>, automatically scoping every query to the
    /// current tenant via <see cref="ITenantContext.TenantId"/>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="tenantContext">The current tenant context, injected per request.</param>
    /// <example>
    /// <code>
    /// // In AtlasDbContextBase.OnModelCreating:
    /// modelBuilder.ApplyTenantFilter(_tenantContext);
    ///
    /// // Effect: every query against ITenantAware entities will have
    /// // WHERE TenantId = @currentTenantId added automatically.
    /// </code>
    /// </example>
    public static ModelBuilder ApplyTenantFilter(
        this ModelBuilder modelBuilder,
        ITenantContext    tenantContext)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantAware).IsAssignableFrom(entityType.ClrType)) continue;
            if (entityType.IsOwned()) continue;

            typeof(ModelBuilderExtensions)
                .GetMethod(nameof(SetTenantFilter),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(null, [modelBuilder, tenantContext]);
        }

        return modelBuilder;
    }

    // ── Private generic filter setters ────────────────────────────────────────

    private static void SetSoftDeleteFilter<T>(ModelBuilder modelBuilder)
        where T : class, ISoftDeletable
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void SetTenantFilter<T>(
        ModelBuilder   modelBuilder,
        ITenantContext tenantContext)
        where T : class, ITenantAware
    {
        // Closure — EF Core evaluates this per query
        modelBuilder.Entity<T>().HasQueryFilter(
            e => e.TenantId == tenantContext.TenantId);
    }
}
