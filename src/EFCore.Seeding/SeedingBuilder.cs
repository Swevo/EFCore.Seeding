using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Swevo.EFCore.Seeding;

/// <summary>Collects seed registrations and their dependency relationships.</summary>
public sealed class SeedingBuilder(IServiceCollection services)
{
    private readonly List<SeedDescriptor> _descriptors = [];

    internal IReadOnlyList<SeedDescriptor> Descriptors => _descriptors;

    /// <summary>
    /// Registers <typeparamref name="TSeed"/> to seed the <typeparamref name="TEntity"/> set.
    /// Returns a <see cref="SeedRegistration"/> to optionally declare dependencies via <c>.DependsOn&lt;T&gt;()</c>.
    /// </summary>
    public SeedRegistration Add<TSeed, TEntity>()
        where TSeed : class, IEntitySeed<TEntity>
        where TEntity : class
    {
        services.AddTransient<TSeed>();

        var descriptor = new SeedDescriptor(
            seedType: typeof(TSeed),
            executor: async (sp, ctx, ct) =>
            {
                if (await ctx.Set<TEntity>().AnyAsync(ct))
                    return;

                var seed = sp.GetRequiredService<TSeed>();
                ctx.Set<TEntity>().AddRange(seed.GetData());
                await ctx.SaveChangesAsync(ct);
            });

        _descriptors.Add(descriptor);
        return new SeedRegistration(descriptor);
    }
}
