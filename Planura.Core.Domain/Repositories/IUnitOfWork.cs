using System;
using System.Threading;
using System.Threading.Tasks;

namespace Planura.Core.Domain.Repositories;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IGenericRepository<TEntity, TKey> Repository<TEntity, TKey>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims a deposit payment for a remainder charge: a single conditional UPDATE moving it
    /// from DepositPaid_RemainderDue or RemainderFailed into RemainderCharging. Returns true only for the
    /// one caller whose UPDATE affected the row — this is the mutual-exclusion guard shared by the
    /// background remainder-charge job and the client on-session pay-remainder flow, so exactly one of them
    /// ever charges (no double-charge). Runs outside any explicit transaction so the claim is immediately
    /// visible to the other actor.
    /// </summary>
    Task<bool> TryClaimRemainderChargeAsync(long paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically reclaims a payment stuck in RemainderCharging (e.g. an abandoned on-session SCA) back to
    /// RemainderFailed. Returns true only if the row was still RemainderCharging — so it can never overwrite
    /// a payment that a webhook or the synchronous path already resolved.
    /// </summary>
    Task<bool> TryReclaimStuckRemainderChargeAsync(long paymentId, string reason, CancellationToken cancellationToken = default);
}
