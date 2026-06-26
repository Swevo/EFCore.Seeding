namespace Swevo.EFCore.Seeding;

/// <summary>
/// Provides seed data for an EF Core entity set.
/// The runner checks <c>DbSet&lt;T&gt;.AnyAsync()</c> before inserting —
/// if the set already contains rows the seed is skipped entirely.
/// </summary>
public interface IEntitySeed<T> where T : class
{
    IEnumerable<T> GetData();
}
