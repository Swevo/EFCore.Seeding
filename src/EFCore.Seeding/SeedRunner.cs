using Microsoft.EntityFrameworkCore;

namespace Swevo.EFCore.Seeding;

internal sealed class SeedRunner(
    IReadOnlyList<SeedDescriptor> descriptors,
    IServiceProvider serviceProvider,
    DbContext dbContext) : ISeedRunner
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var ordered = TopologicalSort(descriptors);

        foreach (var descriptor in ordered)
            await descriptor.Executor(serviceProvider, dbContext, cancellationToken);
    }

    private static IReadOnlyList<SeedDescriptor> TopologicalSort(IReadOnlyList<SeedDescriptor> seeds)
    {
        var map        = seeds.ToDictionary(s => s.SeedType);
        var inDegree   = seeds.ToDictionary(s => s.SeedType, _ => 0);
        var dependents = seeds.ToDictionary(s => s.SeedType, _ => new List<Type>());

        foreach (var seed in seeds)
        {
            foreach (var dep in seed.Dependencies)
            {
                if (!map.ContainsKey(dep))
                    throw new InvalidOperationException(
                        $"Seed '{seed.SeedType.Name}' declares a dependency on '{dep.Name}', " +
                        $"but '{dep.Name}' is not registered. Call .Add<{dep.Name}, TEntity>() first.");

                inDegree[seed.SeedType]++;
                dependents[dep].Add(seed.SeedType);
            }
        }

        var queue  = new Queue<Type>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var result = new List<SeedDescriptor>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(map[current]);

            foreach (var dependent in dependents[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }

        if (result.Count != seeds.Count)
            throw new InvalidOperationException(
                "Circular dependency detected in seed configuration. " +
                "Check your .DependsOn<>() declarations.");

        return result;
    }
}
