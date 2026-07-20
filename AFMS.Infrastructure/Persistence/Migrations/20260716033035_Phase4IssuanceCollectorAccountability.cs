using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4IssuanceCollectorAccountability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems");

            migrationBuilder.AddColumn<Guid>(
                name: "BarangayId",
                table: "StockMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BarangayId",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionArea",
                table: "Issuances",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Issuances",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "Issuances",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Issuances",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleasedBy",
                table: "Issuances",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReservationActive",
                table: "IssuanceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "IssuanceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "SeriesYear",
                table: "IssuanceItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BarangayId",
                table: "StockMovements",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_BarangayId",
                table: "Issuances",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems",
                column: "AccountableFormBookletId",
                unique: true,
                filter: "IsReservationActive = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IssuanceItem_Quantity",
                table: "IssuanceItems",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IssuanceItem_SeriesYear",
                table: "IssuanceItems",
                sql: "SeriesYear >= 1900 AND SeriesYear <= 9999");

            migrationBuilder.AddForeignKey(
                name: "FK_Issuances_Barangays_BarangayId",
                table: "Issuances",
                column: "BarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Barangays_BarangayId",
                table: "StockMovements",
                column: "BarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issuances_Barangays_BarangayId",
                table: "Issuances");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Barangays_BarangayId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BarangayId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Issuances_BarangayId",
                table: "Issuances");

            migrationBuilder.DropIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IssuanceItem_Quantity",
                table: "IssuanceItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IssuanceItem_SeriesYear",
                table: "IssuanceItems");

            migrationBuilder.DropColumn(
                name: "BarangayId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "BarangayId",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "CollectionArea",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "ReleasedBy",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "IsReservationActive",
                table: "IssuanceItems");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "IssuanceItems");

            migrationBuilder.DropColumn(
                name: "SeriesYear",
                table: "IssuanceItems");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems",
                column: "AccountableFormBookletId");
        }
    }
}
