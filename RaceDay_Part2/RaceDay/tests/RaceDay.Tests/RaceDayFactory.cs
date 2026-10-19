using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

/// <summary>
/// Boots the real API in memory but swaps SQL Server for an isolated in-memory database,
/// so tests exercise real controllers, session handling and role checks without a database server.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>)
                                            || d.ServiceType == typeof(DbContextOptions)).ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}
