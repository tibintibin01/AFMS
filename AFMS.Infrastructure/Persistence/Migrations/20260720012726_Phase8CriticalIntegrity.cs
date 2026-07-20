using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8CriticalIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountabilityRanges_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "AccountabilityRanges");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveDate",
                table: "StockMovements",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InventoryQuantityDelta",
                table: "StockMovements",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // Preserve the official transaction date and the signed inventory
            // effect of movements already present in an upgraded database.
            migrationBuilder.Sql(
                """
                UPDATE StockMovements
                SET EffectiveDate = COALESCE(
                    CASE
                        WHEN ReferenceType LIKE 'StockReceipt%' THEN
                            (SELECT ReceivedDate FROM StockReceipts WHERE Id = StockMovements.ReferenceId)
                        WHEN ReferenceType LIKE 'Issuance%' THEN
                            (SELECT IssuedDate FROM Issuances WHERE Id = StockMovements.ReferenceId)
                        WHEN ReferenceType LIKE 'UsageReport%' THEN
                            (SELECT ReportingDate FROM UsageReports WHERE Id = StockMovements.ReferenceId)
                        WHEN ReferenceType LIKE 'Transfer%' THEN
                            (SELECT TransferDate FROM Transfers WHERE Id = StockMovements.ReferenceId)
                        WHEN ReferenceType LIKE 'ReturnTransaction%' THEN
                            (SELECT ReturnDate FROM ReturnTransactions WHERE Id = StockMovements.ReferenceId)
                        WHEN ReferenceType LIKE 'FormException%' THEN
                            (SELECT DateDiscovered FROM FormExceptions WHERE Id = StockMovements.ReferenceId)
                    END,
                    substr(OccurredAt, 1, 10),
                    date('now'))
                WHERE EffectiveDate IS NULL;

                UPDATE StockMovements
                SET InventoryQuantityDelta = CASE
                    WHEN MovementType = 1 THEN Quantity
                    WHEN MovementType IN (3, 7, 8) THEN -Quantity
                    WHEN MovementType = 5 AND ReferenceType LIKE 'ReturnTransaction%'
                        AND COALESCE((SELECT Disposition FROM ReturnTransactions WHERE Id = StockMovements.ReferenceId), 1) <> 1
                        THEN -Quantity
                    WHEN MovementType = 6 AND ReferenceType LIKE 'ReturnTransaction%' THEN -Quantity
                    WHEN MovementType = 9 AND ReferenceType LIKE 'StockReceiptReversal%' THEN -Quantity
                    WHEN MovementType = 9 AND (ReferenceType LIKE 'UsageReportReversal%' OR ReferenceType LIKE 'FormExceptionReversal%') THEN Quantity
                    WHEN MovementType = 9 AND ReferenceType LIKE 'ReturnTransactionReversal%'
                        AND COALESCE((SELECT Disposition FROM ReturnTransactions WHERE Id = StockMovements.ReferenceId), 1) <> 1
                        THEN Quantity
                    ELSE 0
                END;
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EffectiveDate",
                table: "StockMovements",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_EffectiveDate_MovementType",
                table: "StockMovements",
                columns: new[] { "EffectiveDate", "MovementType" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_EffectiveDate",
                table: "StockMovements",
                sql: "EffectiveDate >= '1900-01-01'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovement_InventoryDelta",
                table: "StockMovements",
                sql: "InventoryQuantityDelta >= -Quantity AND InventoryQuantityDelta <= Quantity");

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "AccountabilityRanges",
                columns: new[] { "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true,
                filter: "IsActive = 1");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_EffectiveDate_MovementType",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_EffectiveDate",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovement_InventoryDelta",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_AccountabilityRanges_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "AccountabilityRanges");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "InventoryQuantityDelta",
                table: "StockMovements");

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "AccountabilityRanges",
                columns: new[] { "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true);
        }
    }
}
