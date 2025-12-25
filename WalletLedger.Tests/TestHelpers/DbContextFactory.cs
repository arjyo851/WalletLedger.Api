using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WalletLedger.Api.Data;

namespace WalletLedger.Tests.TestHelpers
{


    public static class DbContextFactory
    {
        public static WalletLedgerDbContext Create()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<WalletLedgerDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new WalletLedgerDbContext(options);
            context.Database.EnsureCreated();

            return context;
        }
    }

}
