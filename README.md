# Swevo.EFCore.Seeding

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.Seeding
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Seeding.svg)](https://www.nuget.org/packages/Swevo.EFCore.Seeding).svg)](https://www.nuget.org/packages/Swevo.EFCore.Seeding)
[![CI](https://github.com/Swevo/EFCore.Seeding/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/EFCore.Seeding/actions)

Fluent, idempotent, dependency-ordered seed data for EF Core. Define `IEntitySeed<T>` classes, declare run-order dependencies, then call `SeedDatabaseAsync()` on startup. Seeds that find existing data skip themselves automatically.

```csharp
// 1. Define seeds
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

// 2. Register
builder.Services.AddEFCoreSeeding<AppDbContext>(seeds =>
{
    seeds.Add<RolesSeed, Role>();
    seeds.Add<UsersSeed, User>().DependsOn<RolesSeed>(); // roles run first
});

// 3. Apply once the host is built
await app.SeedDatabaseAsync();
```

## Install

```bash
dotnet add package Swevo.EFCore.Seeding
```

## How idempotency works

Before inserting, the runner calls `DbSet<T>.AnyAsync()`. If the set already contains rows the entire seed is skipped. This means:

- Safe to call `SeedDatabaseAsync()` on every startup
- Re-deploying never duplicates reference data
- Production and test environments stay in sync

## Dependency ordering

Declare which seeds must complete before another starts using `.DependsOn<TSeed>()`:

```csharp
seeds.Add<PermissionsSeed, Permission>();
seeds.Add<RolesSeed, Role>();
seeds.Add<UserRoleSeed, UserRole>()
    .DependsOn<PermissionsSeed>()
    .DependsOn<RolesSeed>();
```

The runner performs a **topological sort** (Kahn's algorithm) and executes seeds in a valid order. Circular dependencies throw `InvalidOperationException` at startup, not at runtime.

## Unregistered dependency detection

```csharp
seeds.Add<UsersSeed, User>().DependsOn<RolesSeed>(); // RolesSeed not registered → throws
```

```
InvalidOperationException: Seed 'UsersSeed' declares a dependency on 'RolesSeed',
but 'RolesSeed' is not registered. Call .Add<RolesSeed, Role>() first.
```

## Manual trigger

If you prefer to seed on demand rather than at startup:

```csharp
using var scope = app.Services.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<ISeedRunner>();
await runner.RunAsync();
```

## Testing

The runner is registered as `ISeedRunner` — mock or stub it in unit tests:

```csharp
// In integration tests with EF Core InMemory:
builder.Services.AddEFCoreSeeding<TestDbContext>(seeds =>
    seeds.Add<RolesSeed, Role>());

await host.SeedDatabaseAsync();
// db.Roles is now populated
```

## Part of the Swevo EF Core toolkit

| Package | Purpose |
|---|---|
| [Swevo.EFCore.Seeding](https://github.com/Swevo/EFCore.Seeding) | This package |
| [Swevo.EFCore.StronglyTyped](https://github.com/Swevo/EFCore.StronglyTyped) | Strongly-typed IDs |
| [Swevo.AutoAudit](https://github.com/Swevo/AutoAudit) | Audit fields |
| [Swevo.EFCore.SoftDelete](https://github.com/Swevo/EFCore.SoftDelete) | Soft delete + global query filter |
| [Swevo.EFCore.Outbox](https://github.com/Swevo/EFCore.Outbox) | Transactional outbox |
| [Swevo.EFCore.Pagination](https://github.com/Swevo/EFCore.Pagination) | Offset + cursor pagination |


## Also by the same author

> 🌐 Full suite overview: **[swevo.github.io](https://swevo.github.io/)**

| Package | Description |
|---|---|
| [**AutoLog.Generator**](https://github.com/Swevo/AutoLog.Generator) | Compile-time high-performance logging — `[Log(Level, Message)]` generates `LoggerMessage.Define`. AOT-safe. |
| [**AutoHttpClient.Generator**](https://github.com/Swevo/AutoHttpClient.Generator) | Compile-time typed HTTP client — `[HttpClient]` on an interface generates a strongly-typed client. AOT-safe Refit alternative. |
| [**AutoDispatch.Generator**](https://github.com/Swevo/AutoDispatch.Generator) | Compile-time CQRS dispatcher — `[Handler]` generates a strongly-typed `IDispatcher`. No MediatR, no reflection. |
| [**AutoWire**](https://github.com/Swevo/AutoWire) | Compile-time DI auto-registration — `[Scoped]`/`[Singleton]`/`[Transient]` generates `IServiceCollection` registration code. |
| [**AutoMap.Generator**](https://github.com/Swevo/AutoMap.Generator) | Compile-time object mapping with generated extension methods. AOT-safe AutoMapper alternative. |
## License

MIT © 2026 Justin Bannister
