using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3StockReceivingInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_ReceivedDate_Status",
                table: "StockReceipts");

            migrationBuilder.DropIndex(
                name: "IX_AccountableFormBooklets_AccountableFormTypeId_BeginningSerial_EndingSerial",
                table: "AccountableFormBooklets");

            migrationBuilder.DropIndex(
                name: "IX_AccountableFormBooklets_StockReceiptItemId",
                table: "AccountableFormBooklets");

            migrationBuilder.AlterColumn<Guid>(
                name: "FiscalYearId",
                table: "StockReceipts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountableFormTypeId",
                table: "StockReceipts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EncodedByUserId",
                table: "StockReceipts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "StockReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "StockReceipts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "StockReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesYear",
                table: "StockReceipts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceDocumentNumber",
                table: "StockReceipts",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocation",
                table: "StockReceipts",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "StockReceipts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBooklets",
                table: "StockReceiptItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptsPerBooklet",
                table: "StockReceiptItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountableFormTypeId",
                table: "StockMovements",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Destination",
                table: "StockMovements",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "StockMovements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "StockMovements",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "StockMovements",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookletSequence",
                table: "AccountableFormBooklets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentBarangayId",
                table: "AccountableFormBooklets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateIssued",
                table: "AccountableFormBooklets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateReceived",
                table: "AccountableFormBooklets",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<long>(
                name: "RemainingQuantity",
                table: "AccountableFormBooklets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AccountableFormBooklets",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesYear",
                table: "AccountableFormBooklets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StorageLocation",
                table: "AccountableFormBooklets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalQuantity",
                table: "AccountableFormBooklets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UsedQuantity",
                table: "AccountableFormBooklets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ReferenceNumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    LastNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceNumberSequences", x => x.Id);
                    table.CheckConstraint("CK_ReferenceNumberSequence_Number", "LastNumber > 0");
                    table.CheckConstraint("CK_ReferenceNumberSequence_Year", "Year >= 1900 AND Year <= 9999");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_AccountableFormTypeId",
                table: "StockReceipts",
                column: "AccountableFormTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_EncodedByUserId",
                table: "StockReceipts",
                column: "EncodedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ReceivedDate_Status_SeriesYear",
                table: "StockReceipts",
                columns: new[] { "ReceivedDate", "Status", "SeriesYear" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockReceipt_SeriesYear",
                table: "StockReceipts",
                sql: "SeriesYear >= 1900 AND SeriesYear <= 9999");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockReceiptItem_Booklets",
                table: "StockReceiptItems",
                sql: "NumberOfBooklets > 0 AND ReceiptsPerBooklet > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockReceiptItem_CalculatedQuantity",
                table: "StockReceiptItems",
                sql: "Quantity = (EndingSerial - BeginningSerial + 1) AND Quantity = (NumberOfBooklets * ReceiptsPerBooklet)");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_AccountableFormTypeId",
                table: "StockMovements",
                column: "AccountableFormTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId_AccountableFormBookletId_MovementType",
                table: "StockMovements",
                columns: new[] { "ReferenceType", "ReferenceId", "AccountableFormBookletId", "MovementType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_Quantity",
                table: "StockMovements",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_AccountableFormTypeId_SeriesYear_BeginningSerial_EndingSerial",
                table: "AccountableFormBooklets",
                columns: new[] { "AccountableFormTypeId", "SeriesYear", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_CurrentBarangayId",
                table: "AccountableFormBooklets",
                column: "CurrentBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_StockReceiptItemId_BookletSequence",
                table: "AccountableFormBooklets",
                columns: new[] { "StockReceiptItemId", "BookletSequence" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountableFormBooklet_Quantities",
                table: "AccountableFormBooklets",
                sql: "TotalQuantity > 0 AND UsedQuantity >= 0 AND RemainingQuantity >= 0 AND UsedQuantity + RemainingQuantity = TotalQuantity");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountableFormBooklet_Sequence",
                table: "AccountableFormBooklets",
                sql: "BookletSequence > 0 AND SeriesYear >= 1900 AND SeriesYear <= 9999");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceNumberSequences_IsActive",
                table: "ReferenceNumberSequences",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceNumberSequences_Prefix_Year",
                table: "ReferenceNumberSequences",
                columns: new[] { "Prefix", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountableFormBooklets_Barangays_CurrentBarangayId",
                table: "AccountableFormBooklets",
                column: "CurrentBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_AccountableFormTypes_AccountableFormTypeId",
                table: "StockMovements",
                column: "AccountableFormTypeId",
                principalTable: "AccountableFormTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockReceipts_AccountableFormTypes_AccountableFormTypeId",
                table: "StockReceipts",
                column: "AccountableFormTypeId",
                principalTable: "AccountableFormTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockReceipts_Users_EncodedByUserId",
                table: "StockReceipts",
                column: "EncodedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountableFormBooklets_Barangays_CurrentBarangayId",
                table: "AccountableFormBooklets");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_AccountableFormTypes_AccountableFormTypeId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockReceipts_AccountableFormTypes_AccountableFormTypeId",
                table: "StockReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_StockReceipts_Users_EncodedByUserId",
                table: "StockReceipts");

            migrationBuilder.DropTable(
                name: "ReferenceNumberSequences");

            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_AccountableFormTypeId",
                table: "StockReceipts");

            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_EncodedByUserId",
                table: "StockReceipts");

            migrationBuilder.DropIndex(
                name: "IX_StockReceipts_ReceivedDate_Status_SeriesYear",
                table: "StockReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockReceipt_SeriesYear",
                table: "StockReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockReceiptItem_Booklets",
                table: "StockReceiptItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockReceiptItem_CalculatedQuantity",
                table: "StockReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_AccountableFormTypeId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId_AccountableFormBookletId_MovementType",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_Quantity",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_AccountableFormBooklets_AccountableFormTypeId_SeriesYear_BeginningSerial_EndingSerial",
                table: "AccountableFormBooklets");

            migrationBuilder.DropIndex(
                name: "IX_AccountableFormBooklets_CurrentBarangayId",
                table: "AccountableFormBooklets");

            migrationBuilder.DropIndex(
                name: "IX_AccountableFormBooklets_StockReceiptItemId_BookletSequence",
                table: "AccountableFormBooklets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountableFormBooklet_Quantities",
                table: "AccountableFormBooklets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountableFormBooklet_Sequence",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "AccountableFormTypeId",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "EncodedByUserId",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "SeriesYear",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "SourceDocumentNumber",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "StorageLocation",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "StockReceipts");

            migrationBuilder.DropColumn(
                name: "NumberOfBooklets",
                table: "StockReceiptItems");

            migrationBuilder.DropColumn(
                name: "ReceiptsPerBooklet",
                table: "StockReceiptItems");

            migrationBuilder.DropColumn(
                name: "AccountableFormTypeId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Destination",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BookletSequence",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "CurrentBarangayId",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "DateIssued",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "DateReceived",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "RemainingQuantity",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "SeriesYear",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "StorageLocation",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "TotalQuantity",
                table: "AccountableFormBooklets");

            migrationBuilder.DropColumn(
                name: "UsedQuantity",
                table: "AccountableFormBooklets");

            migrationBuilder.AlterColumn<Guid>(
                name: "FiscalYearId",
                table: "StockReceipts",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ReceivedDate_Status",
                table: "StockReceipts",
                columns: new[] { "ReceivedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_AccountableFormTypeId_BeginningSerial_EndingSerial",
                table: "AccountableFormBooklets",
                columns: new[] { "AccountableFormTypeId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_StockReceiptItemId",
                table: "AccountableFormBooklets",
                column: "StockReceiptItemId");
        }
    }
}
