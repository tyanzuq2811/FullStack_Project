using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectIdToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "123_Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_123_Bookings_ProjectId",
                table: "123_Bookings",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_123_Bookings_123_Projects_ProjectId",
                table: "123_Bookings",
                column: "ProjectId",
                principalTable: "123_Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_123_Bookings_123_Projects_ProjectId",
                table: "123_Bookings");

            migrationBuilder.DropIndex(
                name: "IX_123_Bookings_ProjectId",
                table: "123_Bookings");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "123_Bookings");
        }
    }
}
