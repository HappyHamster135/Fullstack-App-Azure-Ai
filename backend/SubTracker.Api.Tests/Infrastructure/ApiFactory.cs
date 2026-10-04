using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SubTracker.Api.Data;

namespace SubTracker.Api.Tests.Infrastructure;

/// <summary>
/// Startar hela API:t i minnet men byter SQL Server mot en delad SQLite-databas i minnet,
/// så att tester kan köra riktiga HTTP-anrop (JWT, controllers, services, EF Core) utan någon databasserver.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Databasen i minnet finns bara så länge anslutningen är öppen.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>Klockan som API:t ser. Tester som beror på datum sätter den själva i början av testet.</summary>
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Samma inställningar som kommer från user-secrets/App Service i riktig drift.
        // Nyckeln är en engångsnyckel för tester och skyddar ingenting.
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");
        builder.UseSetting("Jwt:Key", "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Database:MigrateOnStartup", "false");

        builder.ConfigureServices(services =>
        {
            var registrations = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();

            foreach (var registration in registrations)
            {
                services.Remove(registration);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _connection.Open();

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
