using ATLAS.Kernel.Abstractions.Interfaces.Infrastructure;
using ATLAS.Kernel.Database.Client.Client;
using ATLAS.Kernel.Database.Client.UI;
using ATLAS.Kernel.Database.Configuration;
using ATLAS.Kernel.Database.Interceptors;
using ATLAS.Kernel.Database.UnitOfWork;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

// ── Bootstrap logger mínimo ────────────────────────────────────────────────────
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "[HH:mm:ss] "; })
     .SetMinimumLevel(LogLevel.Warning));   // Solo warnings y errores — SlowQuery los emitirá aquí

// ── Configuración ──────────────────────────────────────────────────────────────
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// ── Selección de proveedor ─────────────────────────────────────────────────────
AnsiConsole.Clear();
AnsiConsole.Write(new Rule("[bold blue]ATLAS.Kernel.Database — Demo[/]").RuleStyle(Style.Parse("blue")));

var providerChoice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("Selecciona el [bold]proveedor de base de datos[/]:")
        .AddChoices(
            "PostgreSQL   — Npgsql.EntityFrameworkCore.PostgreSQL",
            "SQL Server   — Microsoft.EntityFrameworkCore.SqlServer",
            "MySQL        — Pomelo.EntityFrameworkCore.MySql"));

var selectedProvider = providerChoice switch
{
    var s when s.StartsWith("PostgreSQL") => DatabaseProvider.PostgreSql,
    var s when s.StartsWith("SQL Server") => DatabaseProvider.SqlServer,
    var s when s.StartsWith("MySQL")      => DatabaseProvider.MySql,
    _                                      => DatabaseProvider.PostgreSql
};

var providerKey = selectedProvider switch
{
    DatabaseProvider.PostgreSql => "PostgreSql",
    DatabaseProvider.SqlServer  => "SqlServer",
    DatabaseProvider.MySql      => "MySql",
    _ => "PostgreSql"
};

var connectionString = configuration[$"Providers:{providerKey}:ConnectionString"]
    ?? throw new InvalidOperationException(
        $"Falta Providers:{providerKey}:ConnectionString en appsettings.json");

AnsiConsole.MarkupLine($"[green]✔[/] Proveedor seleccionado: [cyan]{selectedProvider}[/]");

// ── DI Container ───────────────────────────────────────────────────────────────
var services = new ServiceCollection();

// Logging
services.AddSingleton(loggerFactory);
services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

// AtlasDatabaseOptions — se construye a partir de la sección Atlas:Database
// y se sobreescribe Provider y ConnectionString con la selección del usuario
services.Configure<AtlasDatabaseOptions>(opts =>
{
    configuration.GetSection(AtlasDatabaseOptions.SectionKey).Bind(opts);
    opts.Provider         = selectedProvider;
    opts.ConnectionString = connectionString;
});

// Stubs de infraestructura
services.AddSingleton<ConsoleCurrentUser>();
services.AddSingleton<ICurrentUser>(sp => sp.GetRequiredService<ConsoleCurrentUser>());

services.AddSingleton<ConsoleTenantContext>();
services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<ConsoleTenantContext>());

services.AddSingleton<ConsoleDateTimeProvider>();
services.AddSingleton<IDateTimeProvider>(sp => sp.GetRequiredService<ConsoleDateTimeProvider>());

services.AddSingleton<ConsoleDomainEventPublisher>();
services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<ConsoleDomainEventPublisher>());

// SlowQueryInterceptor (singleton — sin estado salvo el umbral estático)
services.AddSingleton<SlowQueryInterceptor>();

// DbContext (scoped — se crea una vez por scope; en consola usamos scope único)
services.AddDbContext<ClientDbContext>();

// UnitOfWork
services.AddScoped<UnitOfWork<ClientDbContext>>();

// Menú
services.AddScoped<ConsoleMenu>(sp => new ConsoleMenu(
    sp.GetRequiredService<ClientDbContext>(),
    sp.GetRequiredService<UnitOfWork<ClientDbContext>>(),
    sp.GetRequiredService<ConsoleCurrentUser>(),
    sp.GetRequiredService<ConsoleTenantContext>(),
    sp.GetRequiredService<ConsoleDateTimeProvider>(),
    sp.GetRequiredService<IOptions<AtlasDatabaseOptions>>().Value));

//  Ejecutar en un scope único 
await using var sp = services.BuildServiceProvider();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var scope = sp.CreateScope();

try
{
    var menu = scope.ServiceProvider.GetRequiredService<ConsoleMenu>();
    await menu.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    AnsiConsole.MarkupLine("[grey]Operación cancelada.[/]");
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes);
    return 1;
}

AnsiConsole.MarkupLine("[grey]¡Hasta luego![/]");
return 0;
