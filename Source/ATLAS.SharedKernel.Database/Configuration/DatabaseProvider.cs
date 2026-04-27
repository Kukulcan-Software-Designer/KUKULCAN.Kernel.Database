namespace ATLAS.SharedKernel.Database.Configuration;

/// <summary>
/// Identifies the relational database engine used by an ATLAS module's DbContext.
/// The provider is configured via the <c>Atlas:Database:Provider</c> key in
/// <c>appsettings.json</c> and read by <see cref="AtlasDatabaseOptions"/>.
/// </summary>
/// <remarks>
/// Provider-specific NuGet packages must be added to the consuming project:
/// <list type="table">
///   <listheader><term>Value</term><description>Required package</description></listheader>
///   <item><term>SqlServer</term> <description>Microsoft.EntityFrameworkCore.SqlServer</description></item>
///   <item><term>PostgreSql</term><description>Npgsql.EntityFrameworkCore.PostgreSQL</description></item>
///   <item><term>MySql</term>    <description>Pomelo.EntityFrameworkCore.MySql</description></item>
///   <item><term>Oracle</term>   <description>Oracle.EntityFrameworkCore</description></item>
///   <item><term>Sqlite</term>   <description>Microsoft.EntityFrameworkCore.Sqlite (dev/test only)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // appsettings.json
/// {
///   "Atlas": {
///     "Database": { "Provider": "PostgreSql" }
///   }
/// }
/// </code>
/// </example>
public enum DatabaseProvider
{
    /// <summary>
    /// Microsoft SQL Server 2019+.
    /// Uses <c>UseSqlServer()</c> with native resilience strategy.
    /// Recommended for Azure and Windows environments.
    /// </summary>
    SqlServer = 0,

    /// <summary>
    /// PostgreSQL 14+ via the Npgsql provider.
    /// Uses <c>UseNpgsql()</c> with native resilience strategy.
    /// Recommended for Linux / cloud-neutral deployments.
    /// </summary>
    PostgreSql = 1,

    /// <summary>
    /// MySQL 8+ or MariaDB 10.6+ via the Pomelo provider.
    /// Uses <c>UseMySql()</c> with automatic server-version detection.
    /// </summary>
    MySql = 2,

    /// <summary>
    /// Oracle Database 19c+ via the official Oracle EF Core provider.
    /// Uses <c>UseOracle()</c>. Requires a valid Oracle license.
    /// </summary>
    Oracle = 3,

    /// <summary>
    /// SQLite via Microsoft's provider.
    /// Intended <b>only</b> for local development and integration tests.
    /// Not supported in production deployments.
    /// </summary>
    Sqlite = 4,
}
