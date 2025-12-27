namespace WalletLedger.Api.Auth
{
    public static class Permissions
    {
        public const string WalletRead = "wallet.read";
        public const string WalletWrite = "wallet.write";

        public const string TransactionCredit = "transaction.credit";
        public const string TransactionDebit = "transaction.debit";

        public const string AdminHealth = "admin.health";
    }
}
