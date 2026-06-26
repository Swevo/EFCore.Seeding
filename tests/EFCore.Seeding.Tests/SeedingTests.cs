using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Swevo.EFCore.Seeding;
using Xunit;

namespace EFCore.Seeding.Tests;

// ── Test model ────────────────────────────────────────────────────────────────

public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public int RoleId { get; set; }
}

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
}

// ── Seeds ─────────────────────────────────────────────────────────────────────

public class RolesSeed : IEntitySeed<Role>
{
    public IEnumerable<Role> GetData() =>
    [
        new Role { Id = 1, Name = "Admin" },
        new Role { Id = 2, Name = "User" },
    ];
}

public class UsersSeed : IEntitySeed<User>
{
    public IEnumerable<User> GetData() =>
    [
        new User { Id = 1, Email = "admin@example.com", RoleId = 1 },
    ];
}

public class PermissionsSeed : IEntitySeed<Permission>
{
    public IEnumerable<Permission> GetData() =>
    [
        new Permission { Id = 1, Name = "Read" },
        new Permission { Id = 2, Name = "Write" },
    ];
}

public class EmptyRolesSeed : IEntitySeed<Role>
{
    public IEnumerable<Role> GetData() => [];
}

// ── Helper ────────────────────────────────────────────────────────────────────

file static class Helpers
{
    public static IServiceProvider BuildProvider(
        string dbName,
        Action<SeedingBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddEFCoreSeeding<TestDbContext>(configure);
        return services.BuildServiceProvider();
    }

    public static async Task<ISeedRunner> GetRunner(IServiceProvider sp)
    {
        // Ensure schema exists
        var ctx = sp.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        return sp.GetRequiredService<ISeedRunner>();
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class SeedRunnerTests : IAsyncDisposable
{
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _sp;

    public SeedRunnerTests()
    {
        var root = Helpers.BuildProvider(
            Guid.NewGuid().ToString(),
            seeds => seeds.Add<RolesSeed, Role>());
        _scope = root.CreateScope();
        _sp = _scope.ServiceProvider;
    }

    public async ValueTask DisposeAsync()
    {
        if (_scope is IAsyncDisposable ad)
            await ad.DisposeAsync();
        else
            _scope.Dispose();
    }

    [Fact]
    public async Task Seed_WhenTableIsEmpty_InsertsData()
    {
        var runner = await Helpers.GetRunner(_sp);
        await runner.RunAsync();

        var ctx = _sp.GetRequiredService<TestDbContext>();
        var roles = await ctx.Roles.ToListAsync();
        roles.Should().HaveCount(2);
    }

    [Fact]
    public async Task Seed_WhenTableAlreadyHasData_SkipsInsert()
    {
        var ctx = _sp.GetRequiredService<TestDbContext>();
        ctx.Roles.Add(new Role { Id = 99, Name = "Existing" });
        await ctx.SaveChangesAsync();

        var runner = await Helpers.GetRunner(_sp);
        await runner.RunAsync();

        var count = await ctx.Roles.CountAsync();
        count.Should().Be(1); // only the pre-existing row
    }

    [Fact]
    public async Task Seed_RunTwice_DoesNotDuplicate()
    {
        var runner = await Helpers.GetRunner(_sp);
        await runner.RunAsync();
        await runner.RunAsync();

        var ctx = _sp.GetRequiredService<TestDbContext>();
        var count = await ctx.Roles.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Seed_WithEmptyData_DoesNotThrow()
    {
        var root = Helpers.BuildProvider(
            Guid.NewGuid().ToString(),
            seeds => seeds.Add<EmptyRolesSeed, Role>());
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var runner = await Helpers.GetRunner(sp);
        var act = async () => await runner.RunAsync();
        await act.Should().NotThrowAsync();
    }
}

public class DependencyOrderingTests
{
    [Fact]
    public async Task DependencyOrdering_RolesSeededBeforeUsers()
    {
        var executionOrder = new List<string>();

        // Custom seeds that record execution order
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddEFCoreSeeding<TestDbContext>(seeds =>
        {
            seeds.Add<RolesSeed, Role>();
            seeds.Add<UsersSeed, User>().DependsOn<RolesSeed>();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var innerSp = scope.ServiceProvider;

        var ctx = innerSp.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var runner = innerSp.GetRequiredService<ISeedRunner>();
        await runner.RunAsync();

        var roles = await ctx.Roles.ToListAsync();
        var users = await ctx.Users.ToListAsync();
        roles.Should().HaveCount(2);
        users.Should().HaveCount(1);
    }

    [Fact]
    public async Task MultipleDependencies_AllSeedsRun()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddEFCoreSeeding<TestDbContext>(seeds =>
        {
            seeds.Add<RolesSeed, Role>();
            seeds.Add<PermissionsSeed, Permission>();
            seeds.Add<UsersSeed, User>()
                .DependsOn<RolesSeed>()
                .DependsOn<PermissionsSeed>();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var innerSp = scope.ServiceProvider;

        var ctx = innerSp.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var runner = innerSp.GetRequiredService<ISeedRunner>();
        await runner.RunAsync();

        (await ctx.Roles.CountAsync()).Should().Be(2);
        (await ctx.Permissions.CountAsync()).Should().Be(2);
        (await ctx.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CircularDependency_Throws()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        // RolesSeed depends on UsersSeed, UsersSeed depends on RolesSeed → cycle
        services.AddEFCoreSeeding<TestDbContext>(seeds =>
        {
            seeds.Add<RolesSeed, Role>().DependsOn<UsersSeed>();
            seeds.Add<UsersSeed, User>().DependsOn<RolesSeed>();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISeedRunner>();
        var act = async () => await runner.RunAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Circular*");
    }

    [Fact]
    public void UnregisteredDependency_Throws()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddEFCoreSeeding<TestDbContext>(seeds =>
        {
            // UsersSeed depends on RolesSeed but RolesSeed is not registered
            seeds.Add<UsersSeed, User>().DependsOn<RolesSeed>();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var innerSp = scope.ServiceProvider;

        var ctx = innerSp.GetRequiredService<TestDbContext>();
        var runner = innerSp.GetRequiredService<ISeedRunner>();
        var act = async () => await runner.RunAsync();
        act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not registered*");
    }
}

public class MultipleSeedsTests
{
    [Fact]
    public async Task MultipleSeeds_NoDependencies_AllRun()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddEFCoreSeeding<TestDbContext>(seeds =>
        {
            seeds.Add<RolesSeed, Role>();
            seeds.Add<PermissionsSeed, Permission>();
        });

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var innerSp = scope.ServiceProvider;
        var ctx = innerSp.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var runner = innerSp.GetRequiredService<ISeedRunner>();
        await runner.RunAsync();

        (await ctx.Roles.CountAsync()).Should().Be(2);
        (await ctx.Permissions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Seed_CorrectDataInserted()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddEFCoreSeeding<TestDbContext>(seeds => seeds.Add<RolesSeed, Role>());

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var innerSp = scope.ServiceProvider;
        var ctx = innerSp.GetRequiredService<TestDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        await innerSp.GetRequiredService<ISeedRunner>().RunAsync();

        var admin = await ctx.Roles.SingleAsync(r => r.Id == 1);
        admin.Name.Should().Be("Admin");
    }
}

public class HostExtensionTests
{
    [Fact]
    public async Task SeedDatabaseAsync_ViaIHost_RunsSeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddEFCoreSeeding<TestDbContext>(seeds => seeds.Add<RolesSeed, Role>());
            })
            .Build();

        await host.SeedDatabaseAsync();

        using var scope = host.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        (await ctx.Roles.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task SeedDatabaseAsync_CalledTwice_Idempotent()
    {
        var dbName = Guid.NewGuid().ToString();
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddEFCoreSeeding<TestDbContext>(seeds => seeds.Add<RolesSeed, Role>());
            })
            .Build();

        await host.SeedDatabaseAsync();
        await host.SeedDatabaseAsync();

        using var scope = host.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        (await ctx.Roles.CountAsync()).Should().Be(2);
    }
}
