using ATLAS.Kernel.Abstractions.Interfaces.Infrastructure;
using MediatR;
using Spectre.Console;

namespace ATLAS.Kernel.Database.Client.Client;

// ── ICurrentUser ──────────────────────────────────────────────────────────────
/// <summary>
/// Stub configurable en tiempo de ejecución para simular distintos usuarios.
/// </summary>
public sealed class ConsoleCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; private set; } = true;
    public string UserName { get; private set; } = "demo-user";
    public Guid UserId { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = "demo@atlas.local";
    public IReadOnlyList<string> Roles { get; private set; } = ["Admin"];
    public Guid TenantId { get; private set; } = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public bool IsInRole(string role) => IsAuthenticated && Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool IsInAllRoles(params string[] roles) => IsAuthenticated && roles.All(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));

    public void SetUser(string userName, Guid userId, string email = "demo@atlas.local", IReadOnlyList<string>? roles = null, Guid? tenantId = null)
    {
        IsAuthenticated = true;
        UserName        = userName;
        UserId          = userId;
        Email           = email;
        Roles           = roles ?? ["Admin"];
        TenantId        = tenantId ?? Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    }

    public void SetUnauthenticated()
    {
        IsAuthenticated = false;
        UserName        = string.Empty;
        Email           = string.Empty;
        Roles           = [];
    }
}

// ── ITenantContext ────────────────────────────────────────────────────────────
/// <summary>
/// Stub configurable para simular distintos tenants.
/// </summary>
public sealed class ConsoleTenantContext : ITenantContext
{
    public Guid   TenantId            { get; private set; } = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public string TenantCode          { get; private set; } = "DEMO";
    public string Locale              { get; private set; } = "es-ES";
    public string TimeZoneId          { get; private set; } = "Europe/Madrid";
    public string DefaultCurrencyCode { get; private set; } = "EUR";
    public bool   IsResolved          { get; private set; } = true;

    public void SetTenant(Guid tenantId, string tenantCode = "DEMO", string locale = "es-ES", string timeZoneId = "Europe/Madrid",
        string defaultCurrencyCode = "EUR")
    {
        TenantId            = tenantId;
        TenantCode          = tenantCode;
        Locale              = locale;
        TimeZoneId          = timeZoneId;
        DefaultCurrencyCode = defaultCurrencyCode;
        IsResolved          = true;
    }
}

// ── IDateTimeProvider ─────────────────────────────────────────────────────────
/// <summary>
/// Stub con reloj fijo configurable para reproducir escenarios temporales.
/// </summary>
public sealed class ConsoleDateTimeProvider : IDateTimeProvider
{
    private DateTimeOffset? _fixedTime;
    public DateTimeOffset UtcNow => _fixedTime ?? DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
    public long UnixTimestampSeconds => UtcNow.ToUnixTimeSeconds();
    
    public void FixTime(DateTimeOffset time) => _fixedTime = time;
    public void UseRealTime() => _fixedTime = null;
}

// ── IPublisher (MediatR) ──────────────────────────────────────────────────────
/// <summary>
/// Stub que imprime los domain events en consola en lugar de despacharlos
/// a handlers reales. Permite visualizar el DomainEventDispatchInterceptor.
/// </summary>
public sealed class ConsoleDomainEventPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        AnsiConsole.MarkupLine($"  [green]▶ DomainEvent despachado:[/] [yellow]{notification.GetType().Name}[/]");

        // Print public properties
        foreach (var prop in notification.GetType().GetProperties())
        {
            var val = prop.GetValue(notification);
            AnsiConsole.MarkupLine($"    [grey]{prop.Name}:[/] {val?.ToString()?.EscapeMarkup()}");
        }

        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification,
        CancellationToken cancellationToken = default) where TNotification : INotification
        => Publish((object)notification!, cancellationToken);
}