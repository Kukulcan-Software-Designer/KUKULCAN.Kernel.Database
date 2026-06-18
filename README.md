# KUKULCAN.Kernel.Database

## Overview

`KUKULCAN.Kernel.Database` is a foundational library designed to provide shared database abstractions and infrastructure components across the **Kukulcán Software Designer** application ecosystem.
This project acts as part of the **Kernel**, enabling consistency, reuse, and standardization of database-related concerns across multiple modules and bounded contexts.

## Purpose

The main goals of this library are:

- Centralize common database logic
- Provide reusable abstractions for persistence
- Enforce consistency across modules
- Reduce duplication in infrastructure code
- Serve as a base layer for database integrations

## Architecture Context

This project is intended to be used within a **modular architecture** aligned with:

- Domain-Driven Design (DDD)
- Clean Architecture
- CQRS (Command Query Responsibility Segregation)

It typically sits in the **Shared Kernel layer**, meaning:

- It is shared across multiple bounded contexts
- It should remain stable and generic
- It must avoid domain-specific logic

## Project Structure

```
KUKULCAN.Kernel.Database/
│
├── Source/             # Main source code 
├── Source Client/      # Client source code example of use 
├── Tests/              # Unit and integration tests
├── Solution Items/     # Solution configuration files, editing, and GitHub
├── Directory.Build.props
└── Solution file
```

## Key Responsibilities

Although the current implementation is minimal, this library is expected to include:

- Database context base classes
- Common Entity Framework configurations
- Base repository patterns
- Transaction management abstractions
- Connection handling
- Migration utilities (optional)
- Cross-cutting persistence concerns

## Expected Components

Typical components that may be implemented:

### 1. Base DbContext

```csharp
public abstract class KukulcanDbContext : DbContext
{
    protected KukulkanDbContext(DbContextOptions options) : base(options)
    {
    }
}
```

### 2. Repository Abstractions

```csharp
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(Guid id);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
```

### 3. Unit of Work

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

## Usage

This library should be referenced by infrastructure layers of other modules:

```bash
dotnet add reference KUKULCAN.Kernel.Database
```

Example usage in a module:

```csharp
services.AddDbContext<MyModuleDbContext>(options =>
{
    options.UseSqlServer(configuration.GetConnectionString("Default"));
});
```

## Design Principles

- **Low coupling**: No dependency on specific business domains
- **High cohesion**: Focus only on database concerns
- **Extensibility**: Easy to extend in consuming modules
- **Consistency**: Standard patterns across all modules

## Guidelines

- Do not include business logic
- Avoid module-specific dependencies
- Keep APIs stable and backward-compatible
- Document all public abstractions

## Future Improvements

- Add base implementations for repositories
- Introduce audit tracking (CreatedAt, UpdatedAt)
- Soft delete support
- Multi-tenancy support
- Outbox pattern integration
- Migration helpers

## Requirements

- **.NET 10**
- MediatR 12.*
- FluentValidation 11.*
- Microsoft.Extensions.Logging.Abstractions 10.*

## License

This project is owned and maintained by **Kukulcán Software Designer** and is distributed under the **General Public License (GPL)**.

This means the software is free to use, modify, and redistribute, provided that:

- The original copyright notice is preserved.
- Proper attribution is given to the original creators.
- Any derivative work distributed must remain under the same GPL license terms.

For full license terms, see the `LICENSE` file included in this repository.

## Notes

This library is currently in an initial phase and may evolve as the **Kukulcán Software Designer** application ecosystem grows.
