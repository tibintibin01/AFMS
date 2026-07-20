using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10RangeIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems");

            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_IssuanceId_AccountableFormBookletId",
                table: "IssuanceItems");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "IssuanceItems",
                columns: new[] { "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true,
                filter: "IsReservationActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_IssuanceId_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "IssuanceItems",
                columns: new[] { "IssuanceId", "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "IssuanceItems");

            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_IssuanceId_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "IssuanceItems");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems",
                column: "AccountableFormBookletId",
                unique: true,
                filter: "IsReservationActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_IssuanceId_AccountableFormBookletId",
                table: "IssuanceItems",
                columns: new[] { "IssuanceId", "AccountableFormBookletId" },
                unique: true);
        }
    }
}
