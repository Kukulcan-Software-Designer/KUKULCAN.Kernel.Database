using ATLAS.SharedKernel.Database.Configuration;
using ATLAS.SharedKernel.Database.Interceptors;
using ATLAS.SharedKernel.Database.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ATLAS.SharedKernel.Database.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> that register an ATLAS
/// module's DbContext together with all required infrastructure services.
/// </summary>
/// <example>
/// <code>
/// // In a module's Infrastructure DependencyInjection.cs:
/// public static IServiceCollection AddCrmInfrastructure(
///     this IServiceCollection services,
///     IConfiguration          configuration)
/// {
///     services.AddAtlasDbContext&lt;CrmDbContext&gt;(configuration);
///     services.AddScoped&lt;ICustomerRepository, CustomerRepository&gt;();
///     return services;
/// }
/// </code>
/// </example>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a module's <typeparamref name="TContext"/> together with
    /// <see cref="AtlasDatabaseOptions"/>, <see cref="IUnitOfWork"/>, and
    /// all required database infrastructure services.
    /// </summary>
    /// <typeparam name="TContext">
    /// The module's DbContext type. Must inherit from <see cref="AtlasDbContextBase"/>.
    /// </typeparam>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">
    /// The application configuration. The <c>Atlas:Database</c> section
    /// (<see cref="AtlasDatabaseOptions.SectionKey"/>) is read to configure the
    /// provider, connection string, and all options.
    /// </param>
    /// <returns>The <paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>Atlas:Database:ConnectionString</c> is missing or empty.
    /// </exception>
    /// <remarks>
    /// This single call registers:
    /// <list type="bullet">
    ///   <item><see cref="AtlasDatabaseOptions"/> via <c>IOptions&lt;AtlasDatabaseOptions&gt;</c></item>
    ///   <item><typeparamref name="TContext"/> (scoped)</item>
    ///   <item><see cref="IUnitOfWork"/> → <see cref="UnitOfWork{TContext}"/> (scoped)</item>
    ///   <item><see cref="SlowQueryInterceptor"/> (singleton)</item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddAtlasDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration          configuration)
        where TContext : AtlasDbContextBase
    {
        // ① Bind and validate options
        var section = configuration.GetSection(AtlasDatabaseOptions.SectionKey);
        services.Configure<AtlasDatabaseOptions>(section);

        var opts = section.Get<AtlasDatabaseOptions>() ?? new AtlasDatabaseOptions();

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            throw new InvalidOperationException(
                $"Missing required configuration: {AtlasDatabaseOptions.SectionKey}:ConnectionString. " +
                $"Ensure it is set in appsettings.json or environment variables.");

        // ② Register the DbContext (scoped — one per HTTP request / unit of work)
        services.AddDbContext<TContext>();

        // ③ Register IUnitOfWork backed by this specific module's context
        services.AddScoped<IUnitOfWork, UnitOfWork<TContext>>();

        // ④ SlowQueryInterceptor as singleton (stateless — only logs)
        services.AddSingleton<SlowQueryInterceptor>();

        return services;
    }
}
