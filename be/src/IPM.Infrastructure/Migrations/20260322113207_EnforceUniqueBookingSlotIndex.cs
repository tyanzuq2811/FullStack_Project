using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IPM.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueBookingSlotIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_123_Bookings_ResourceId_StartTime_EndTime",
                table: "123_Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_123_Bookings_ResourceId_StartTime_EndTime",
                table: "123_Bookings",
                columns: new[] { "ResourceId", "StartTime", "EndTime" },
                unique: true,
                filter: "[Status] <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_123_Bookings_ResourceId_StartTime_EndTime",
                table: "123_Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_123_Bookings_ResourceId_StartTime_EndTime",
                table: "123_Bookings",
                columns: new[] { "ResourceId", "StartTime", "EndTime" });
        }
    }
}
