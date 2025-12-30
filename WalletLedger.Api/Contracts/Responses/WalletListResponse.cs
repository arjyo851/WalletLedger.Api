namespace WalletLedger.Api.Contracts.Responses;

public record WalletListResponse(
    List<WalletDetailResponse> Wallets,
    int TotalCount
);






