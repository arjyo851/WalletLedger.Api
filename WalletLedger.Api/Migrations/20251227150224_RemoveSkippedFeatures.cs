using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletLedger.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSkippedFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReversedByTransactionId",
                table: "LedgerEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReversedByTransactionId",
                table: "LedgerEntries",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
