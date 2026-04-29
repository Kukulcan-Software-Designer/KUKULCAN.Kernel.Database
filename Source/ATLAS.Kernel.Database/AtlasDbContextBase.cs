using ATLAS.Kernel.Database.Configuration;
using ATLAS.Kernel.Database.Extensions;
using ATLAS.Kernel.Database.Interceptors;
using Microsoft.Extensions.Options;

namespace ATLAS.Kernel.Database;

/// <summary>
/// Abstract base class for all ATLAS module DbContexts.
/// Centralises every cross-cutting persistence concern so that individual module
/// DbContexts only need to declare their own <c>DbSet&lt;T&gt;</c> properties and
/// set their schema in <c>OnModelCreating</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibilities handled by this base class:</b>
/// <list type="bullet">
///   <item>Database provider selection based on <see cref="AtlasDatabaseOptions.Provider"/>.</item>
///   <item>Auto-discovery of all <c>IEntityTypeConfiguration&lt;T&gt;</c> in the calling module's assembly.</item>
///   <item>Global soft-delete query filter (<c>WHERE IsDeleted = false</c>) for <see cref="ISoftDeletable"/> entities.</item>
///   <item>Global tenant isolation filter (<c>WHERE TenantId = @current</c>) for <see cref="ITenantAware"/> entities.</item>
///   <item>Audit field population via <see cref="AuditSaveChangesInterceptor"/>.</item>
///   <item>Soft-delete conversion via <see cref="SoftDeleteInterceptor"/>.</item>
///   <item>Domain event dispatch via <see cref="DomainEventDispatchInterceptor"/>.</item>
///   <item>Immutable entity enforcement via <see cref="ImmutableEntityInterceptor"/>.</item>
///   <item>Slow query logging via <see cref="SlowQueryInterceptor"/>.</item>
/// </list>
/// </para>
/// <para>
/// <b>How to create a module DbContext:</b>
/// <code>
/// public sealed class CrmDbContext : AtlasDbContextBase
/// {
///     public CrmDbContext(
///         IOptions&lt;AtlasDatabaseOptions&gt; options,
///         ITenantContext      tenantContext,
///         ICurrentUser        currentUser,
///         IDateTimeProvider   dateTimeProvider,
///         IPublisher          publisher)
///         : base(options, tenantContext, currentUser, dateTimeProvider, publisher)
///     { }
///
///     public DbSet&lt;Customer&gt; Customers => Set&lt;Customer&gt;();
///     public DbSet&lt;Contact&gt;  Contacts  => Set&lt;Contact&gt;();
///
///     protected override void OnModelCreating(ModelBuilder modelBuilder)
///     {
///         modelBuilder.HasDefaultSchema("crm");
///         base.OnModelCreating(modelBuilder);   // runs auto-discovery + filters
///     }
/// }
/// </code>
/// </para>
/// </remarks>
/// <remarks>
/// Initialises a new instance with all required cross-cutting services.
/// </remarks>
public abstract class AtlasDbContextBase(IOptions<AtlasDatabaseOptions> options, ITenantContext tenantContext,
    ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, IPublisher publisher) : DbContext
{
    private readonly AtlasDatabaseOptions _opts = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ITenantContext _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    private readonly ICurrentUser _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    private readonly IDateTimeProvider _clock = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    private readonly IPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));

    // ── OnConfiguring — provider selection ────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        // Register all five interceptors
        optionsBuilder.AddInterceptors(
            new AuditSaveChangesInterceptor(_currentUser, _clock),
            new SoftDeleteInterceptor(_currentUser, _clock),
            new DomainEventDispatchInterceptor(_publisher),
            new ImmutableEntityInterceptor());

        if (_opts.EnableSensitiveDataLogging)
            optionsBuilder.EnableSensitiveDataLogging();

        if (_opts.EnableDetailedErrors)
            optionsBuilder.EnableDetailedErrors();

        ConfigureProvider(optionsBuilder);
    }

    /// <summary>
    /// Configures the database provider based on <see cref="AtlasDatabaseOptions.Provider"/>.
    /// Override in a derived class to customise provider configuration.
    /// </summary>
    protected virtual void ConfigureProvider(DbContextOptionsBuilder optionsBuilder)
    {
        var connStr  = _opts.ConnectionString;
        var timeout  = _opts.CommandTimeoutSeconds;
        var maxRetry = _opts.Retry.Enabled ? _opts.Retry.MaxRetryCount : 0;
        var maxDelay = TimeSpan.FromSeconds(_opts.Retry.MaxRetryDelaySeconds);

        switch (_opts.Provider)
        {
            case DatabaseProvider.SqlServer:
                ConfigureSqlServer(optionsBuilder, connStr, timeout, maxRetry, maxDelay);
                break;
            case DatabaseProvider.PostgreSql:
                ConfigurePostgreSql(optionsBuilder, connStr, timeout, maxRetry, maxDelay);
                break;
            case DatabaseProvider.MySql:
                ConfigureMySql(optionsBuilder, connStr, timeout);
                break;
            case DatabaseProvider.Oracle:
                ConfigureOracle(optionsBuilder, connStr, timeout);
                break;
            case DatabaseProvider.Sqlite:
                ConfigureSqlite(optionsBuilder, connStr);
                break;
            default:
                throw new NotSupportedException(
                    $"Database provider '{_opts.Provider}' is not supported.");
        }
    }

    // ── Provider-specific configuration ───────────────────────────────────────
    // Each method uses reflection to avoid direct package references in this library.
    // A clear error is thrown when the required NuGet package is missing.

    private static void ConfigureSqlServer(DbContextOptionsBuilder optionsBuilder,
        string connectionString, int timeoutSec, int maxRetry, TimeSpan maxDelay)
    {
        try
        {
            var type = Type.GetType(
                "Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions, " +
                "Microsoft.EntityFrameworkCore.SqlServer") ?? throw NotInstalled("Microsoft.EntityFrameworkCore.SqlServer");

            type.GetMethod("UseSqlServer",
                    [typeof(DbContextOptionsBuilder), typeof(string), typeof(Action<object>)])
                ?.Invoke(null, [optionsBuilder, connectionString,
                    (Action<object>)(o =>
                    {
                        var t = o.GetType();
                        t.GetMethod("CommandTimeout")?.Invoke(o, [timeoutSec]);
                        if (maxRetry > 0)
                            t.GetMethod("EnableRetryOnFailure",
                                    [typeof(int), typeof(TimeSpan), typeof(IEnumerable<int>?)])
                                ?.Invoke(o, [maxRetry, maxDelay, null]);
                    })]);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw NotInstalled("Microsoft.EntityFrameworkCore.SqlServer", ex);
        }
    }

    private static void ConfigurePostgreSql(DbContextOptionsBuilder optionsBuilder,
        string connectionString, int timeoutSec, int maxRetry, TimeSpan maxDelay)
    {
        try
        {
            var type = Type.GetType(
                "Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions, " +
                "Npgsql.EntityFrameworkCore.PostgreSQL") ?? throw NotInstalled("Npgsql.EntityFrameworkCore.PostgreSQL");

            type.GetMethod("UseNpgsql",
                    [typeof(DbContextOptionsBuilder), typeof(string), typeof(Action<object>)])
                ?.Invoke(null, [optionsBuilder, connectionString,
                    (Action<object>)(o =>
                    {
                        var t = o.GetType();
                        t.GetMethod("CommandTimeout")?.Invoke(o, [timeoutSec]);
                        if (maxRetry > 0)
                            t.GetMethod("EnableRetryOnFailure",
                                    [typeof(int), typeof(TimeSpan), typeof(IEnumerable<string>?)])
                                ?.Invoke(o, [maxRetry, maxDelay, null]);
                    })]);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw NotInstalled("Npgsql.EntityFrameworkCore.PostgreSQL", ex);
        }
    }

    private static void ConfigureMySql(DbContextOptionsBuilder optionsBuilder,
        string connectionString, int timeoutSec)
    {
        try
        {
            var type = Type.GetType(
                "Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions, " +
                "Pomelo.EntityFrameworkCore.MySql") ?? throw NotInstalled("Pomelo.EntityFrameworkCore.MySql");

            var svType = Type.GetType(
                "Microsoft.EntityFrameworkCore.ServerVersion, Pomelo.EntityFrameworkCore.MySql");

            var serverVersion = svType
                ?.GetMethod("AutoDetect", [typeof(string)])
                ?.Invoke(null, [connectionString]);

            type.GetMethod("UseMySql",
                    [typeof(DbContextOptionsBuilder), typeof(string), svType!, typeof(Action<object>)])
                ?.Invoke(null, [optionsBuilder, connectionString, serverVersion,
                    (Action<object>)(o =>
                        o.GetType().GetMethod("CommandTimeout")?.Invoke(o, [timeoutSec]))]);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw NotInstalled("Pomelo.EntityFrameworkCore.MySql", ex);
        }
    }

    private static void ConfigureOracle(DbContextOptionsBuilder optionsBuilder,
        string connectionString, int timeoutSec)
    {
        try
        {
            var type = Type.GetType(
                "Microsoft.EntityFrameworkCore.OracleDbContextOptionsExtensions, " +
                "Oracle.EntityFrameworkCore") ?? throw NotInstalled("Oracle.EntityFrameworkCore");

            type.GetMethod("UseOracle",
                    [typeof(DbContextOptionsBuilder), typeof(string), typeof(Action<object>)])
                ?.Invoke(null, [optionsBuilder, connectionString,
                    (Action<object>)(o =>
                        o.GetType().GetMethod("CommandTimeout")?.Invoke(o, [timeoutSec]))]);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw NotInstalled("Oracle.EntityFrameworkCore", ex);
        }
    }

    private static void ConfigureSqlite(DbContextOptionsBuilder optionsBuilder,
        string connectionString)
    {
        try
        {
            var type = Type.GetType(
                "Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions, " +
                "Microsoft.EntityFrameworkCore.Sqlite") ?? throw NotInstalled(
                "Microsoft.EntityFrameworkCore.Sqlite (development/test only)");

            type.GetMethod("UseSqlite",
                    [typeof(DbContextOptionsBuilder), typeof(string), typeof(Action<object>)])
                ?.Invoke(null, [optionsBuilder, connectionString, null]);
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            throw NotInstalled("Microsoft.EntityFrameworkCore.Sqlite", ex);
        }
    }

    private static NotSupportedException NotInstalled(string package, Exception? inner = null)
        => inner is null
            ? new NotSupportedException(
                $"Package '{package}' is not installed. " +
                $"Add it to the consuming module's Infrastructure project.")
            : new NotSupportedException(
                $"Failed to configure provider. " +
                $"Ensure '{package}' is installed in the consuming project.", inner);

    // ── OnModelCreating ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Auto-discover all IEntityTypeConfiguration<T> in the derived module's assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Global soft-delete filter: WHERE IsDeleted = false
        modelBuilder.ApplySoftDeleteFilter();

        // Global tenant isolation filter: WHERE TenantId = @currentTenantId
        modelBuilder.ApplyTenantFilter(_tenantContext);
    }

    // ── SaveChanges ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);

    /// <inheritdoc/>
    public override int SaveChanges()
        => base.SaveChanges();
}
