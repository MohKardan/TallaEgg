namespace Wallet.Core;

public interface IWalletRepository
{
    // Wallet operations
    Task<WalletEntity?> GetWalletAsync(Guid userId, string asset);
    Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId);
    Task<WalletEntity> CreateWalletAsync(WalletEntity wallet);
    Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction? transaction = null);
    Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    
    // Transaction operations
    Task<Transaction> CreateTransactionAsync(Transaction transaction);

    /// <summary>
    /// The transaction already recorded against <paramref name="walletId"/> under
    /// <paramref name="referenceId"/>, or null if that reference has not been used on this
    /// wallet. Scoped to one wallet on purpose: settlement writes four legs under a single
    /// trade id, one per participating wallet, so a reference is only unique together with the
    /// wallet it moved.
    /// </summary>
    Task<Transaction?> FindTransactionByReferenceAsync(Guid walletId, string referenceId);

    /// <summary>
    /// Applies a wallet change and records its transaction, where the transaction's
    /// <c>ReferenceId</c> makes the operation idempotent: if that reference has already been
    /// recorded against this wallet, no balance moves and the original transaction is returned.
    ///
    /// <para>
    /// The same shape as <see cref="SettleTradeAsync"/>, and for the same reason (issue #157):
    /// a repeat is a success that does nothing, never an error an operator has to interpret.
    /// The pre-check is an optimisation; the guarantee is the unique index over
    /// <c>(WalletId, ReferenceId)</c>, which is what makes a concurrent duplicate impossible
    /// rather than merely unlikely.
    /// </para>
    ///
    /// <para>
    /// A transaction with no reference cannot be deduplicated and is simply applied — that is
    /// every lock, unlock and settlement leg, none of which route through here.
    /// </para>
    /// </summary>
    Task<Transaction> ApplyWithIdempotencyAsync(WalletEntity wallet, Transaction transaction);
    Task<WalletTransaction?> GetTransactionAsync(Guid transactionId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId);
    Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction);

    /// <summary>
    /// Atomically settles a matched trade: consumes both sides' locked collateral,
    /// credits each side the asset they bought, and records a Transaction per leg —
    /// all in a single database transaction with one SaveChanges. Idempotent on
    /// <paramref name="tradeId"/>: a second call for the same trade is a no-op.
    /// </summary>
    Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller);
} 