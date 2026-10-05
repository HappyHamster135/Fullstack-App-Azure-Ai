using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SubTracker.Api.Data;

// Kör det riktiga API:t (Program) över riktig HTTP på en SQLite-fil i stället för SQL Server.
// Filen och allt i den raderas vid varje start. En fil (inte en delad anslutning i minnet) behövs eftersom
// dashboarden gör flera anrop samtidigt, och SQLite i minnet med en enda anslutning ger "database is locked".
public class HarnessFactory : WebApplicationFactory<Program>
{
    private static readonly string DbPath = Path.Combine(Path.GetTempPath(), "subtracker-harness.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "DataSource=:memory:");
        builder.UseSetting("Jwt:Key", "harness-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

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

            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={DbPath}"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        foreach (var file in new[] { DbPath, DbPath + "-wal", DbPath + "-shm", DbPath + "-journal" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }
}

public static class HarnessMain
{
    public static async Task Main(string[] args)
    {
        var port = args.Length > 0 ? int.Parse(args[0]) : 5077;

        var factory = new HarnessFactory();
        factory.UseKestrel(port);
        factory.StartServer();

        Console.WriteLine($"READY http://localhost:{port}");
        await Task.Delay(Timeout.Infinite);
    }
}
