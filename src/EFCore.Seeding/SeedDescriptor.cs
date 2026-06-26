using Microsoft.EntityFrameworkCore;

namespace Swevo.EFCore.Seeding;

internal sealed class SeedDescriptor(
    Type seedType,
    Func<IServiceProvider, DbContext, CancellationToken, Task> executor)
{
    public Type SeedType { get; } = seedType;
    public Func<IServiceProvider, DbContext, CancellationToken, Task> Executor { get; } = executor;
    public List<Type> Dependencies { get; } = [];
}
