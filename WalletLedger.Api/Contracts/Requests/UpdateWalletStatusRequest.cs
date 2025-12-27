using WalletLedger.Api.Domain.Entities;

namespace WalletLedger.Api.Contracts.Requests;

public record UpdateWalletStatusRequest(
    WalletStatus Status
);


