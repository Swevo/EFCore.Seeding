namespace Swevo.EFCore.Seeding;

/// <summary>Returned by <see cref="SeedingBuilder.Add{TSeed,TEntity}"/> to chain dependency declarations.</summary>
public sealed class SeedRegistration
{
    private readonly SeedDescriptor _descriptor;

    internal SeedRegistration(SeedDescriptor descriptor) => _descriptor = descriptor;

    /// <summary>Declares that this seed must run after <typeparamref name="TDependency"/> has been applied.</summary>
    public SeedRegistration DependsOn<TDependency>() where TDependency : class
    {
        _descriptor.Dependencies.Add(typeof(TDependency));
        return this;
    }
}
