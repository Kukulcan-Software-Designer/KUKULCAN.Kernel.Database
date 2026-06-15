using KUKULCAN.Kernel.Database.Extensions;
using KUKULCAN.Kernel.Primitives.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace KUKULCAN.Kernel.Database.UnitOfWork;

/// <summary>
/// Generic implementation of <see cref="IUnitOfWork"/> backed by a specific
/// <typeparamref name="TContext"/> (a module's <see cref="KukulcanDbContextBase"/>).
/// Registered automatically by
/// <see cref="ServiceCollectionExtensions.AddAtlasDbContext{TContext}"/>.
/// </summary>
/// <typeparam name="TContext">
/// The module's specific DbContext type, e.g. <c>CrmDbContext</c>, <c>I18nDbContext</c>.
/// </typeparam>
/// <example>
/// <code>
/// // Normal usage — TransactionBehavior wraps the MediatR command automatically:
/// await _supplierRepo.AddAsync(supplier, ct);
/// await _unitOfWork.SaveChangesAsync(ct);
///
/// // Explicit transaction — only needed for cross-aggregate operations
/// // outside the MediatR pipeline:
/// await _unitOfWork.BeginTransactionAsync(ct);
/// try
/// {
///     await _orderRepo.AddAsync(order, ct);
///     _inventoryRepo.SoftDelete(stock);
///     await _unitOfWork.SaveChangesAsync(ct);
///     await _unitOfWork.CommitTransactionAsync(ct);
/// }
/// catch
/// {
///     await _unitOfWork.RollbackTransactionAsync(ct);
///     throw;
/// }
/// </code>
/// </example>
public sealed class UnitOfWork<TContext> : IUnitOfWork where TContext : KukulcanDbContextBase
{
    private readonly TContext               _context;
    private         IDbContextTransaction?  _transaction;

    /// <summary>Initializes the unit of work with the module's DbContext.</summary>
    public UnitOfWork(TContext context)
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException(
                "A transaction is already in progress. Commit or roll back the current transaction before starting a new one.");

        _transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException(
                "No active transaction to commit. Call BeginTransactionAsync before CommitTransactionAsync.");
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <inheritdoc/>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to end transaction. Call BeginTransactionAsync before EndTransactionAsync.");

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    /// <summary>
    /// Explicit transaction management is only needed for cross-aggregate operations outside the MediatR pipeline.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task EndTransactionAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to end transaction. Call BeginTransactionAsync before EndTransactionAsync.");
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    /// <inheritdoc/>
    public void Dispose() => _transaction?.Dispose();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
    }
}
