# Changelog

## [1.0.0] - 2026-06-26

### Added
- `IEntitySeed<T>` interface for defining seed data
- `SeedingBuilder` with fluent `Add<TSeed, TEntity>().DependsOn<TOther>()` API
- `ISeedRunner` / internal `SeedRunner` with topological sort (Kahn's algorithm)
- Idempotency via `DbSet<T>.AnyAsync()` — seeds that find existing rows are skipped
- `AddEFCoreSeeding<TContext>()` DI extension
- `IHost.SeedDatabaseAsync()` extension for startup seeding
- Circular dependency detection throws `InvalidOperationException` at runtime
- Unregistered dependency detection throws at first `RunAsync()` call
