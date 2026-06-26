using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Swevo.EFCore.Seeding;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers seed classes and the seeding infrastructure for <typeparamref name="TContext"/>.
    /// Call <see cref="HostExtensions.SeedDatabaseAsync"/> after the host is built to apply seeds.
    /// </summary>
    public static IServiceCollection AddEFCoreSeeding<TContext>(
        this IServiceCollection services,
        Action<SeedingBuilder> configure)
        where TContext : DbContext
    {
        var builder = new SeedingBuilder(services);
        configure(builder);

        var descriptors = builder.Descriptors.ToList().AsReadOnly();

        services.AddScoped<ISeedRunner>(sp =>
        {
            var ctx = sp.GetRequiredService<TContext>();
            return new SeedRunner(descriptors, sp, ctx);
        });

        return services;
    }
}

public static class HostExtensions
{
    /// <summary>
    /// Creates a DI scope, resolves <see cref="SeedRunner"/>, and runs all registered seeds.
    /// Seeds whose target <c>DbSet</c> already contains rows are skipped automatically.
    /// </summary>
    public static async Task SeedDatabaseAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        using var scope  = host.Services.CreateScope();
        var runner       = scope.ServiceProvider.GetRequiredService<ISeedRunner>();
        await runner.RunAsync(cancellationToken);
    }
}
