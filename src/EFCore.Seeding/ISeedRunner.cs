namespace Swevo.EFCore.Seeding;

public interface ISeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}
