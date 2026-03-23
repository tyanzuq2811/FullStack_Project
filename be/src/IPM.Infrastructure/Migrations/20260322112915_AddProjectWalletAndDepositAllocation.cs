using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectWalletAndDepositAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "123_WalletTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WalletBalance",
                table: "123_Projects",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_123_WalletTransactions_ProjectId",
                table: "123_WalletTransactions",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_123_WalletTransactions_123_Projects_ProjectId",
                table: "123_WalletTransactions",
                column: "ProjectId",
                principalTable: "123_Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_123_WalletTransactions_123_Projects_ProjectId",
                table: "123_WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_123_WalletTransactions_ProjectId",
                table: "123_WalletTransactions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "123_WalletTransactions");

            migrationBuilder.DropColumn(
                name: "WalletBalance",
                table: "123_Projects");
        }
    }
}
