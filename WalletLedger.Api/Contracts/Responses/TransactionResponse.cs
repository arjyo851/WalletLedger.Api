using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Contracts.Responses;

public record TransactionResponse(
    Guid Id,
    Guid WalletId,
    decimal Amount,
    LedgerEntryType Type,
    string ReferenceId,
    TransactionStatus Status,
    DateTime CreatedAt
);

