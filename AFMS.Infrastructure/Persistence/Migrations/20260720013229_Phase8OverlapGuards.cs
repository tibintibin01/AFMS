using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8OverlapGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite has no exclusion constraints. These triggers make overlap
            // protection authoritative even if a caller bypasses the services.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_StockReceiptItems_NoOverlap_Insert
                BEFORE INSERT ON StockReceiptItems
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping stock receipt serial range.')
                    WHERE EXISTS (
                        SELECT 1
                        FROM StockReceiptItems existing
                        JOIN StockReceipts existingReceipt ON existingReceipt.Id = existing.StockReceiptId
                        JOIN StockReceipts newReceipt ON newReceipt.Id = NEW.StockReceiptId
                        WHERE existing.AccountableFormTypeId = NEW.AccountableFormTypeId
                          AND existingReceipt.SeriesYear = newReceipt.SeriesYear
                          AND existingReceipt.Status <> 9
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;

                CREATE TRIGGER TR_StockReceiptItems_NoOverlap_Update
                BEFORE UPDATE OF AccountableFormTypeId, BeginningSerial, EndingSerial, StockReceiptId ON StockReceiptItems
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping stock receipt serial range.')
                    WHERE EXISTS (
                        SELECT 1
                        FROM StockReceiptItems existing
                        JOIN StockReceipts existingReceipt ON existingReceipt.Id = existing.StockReceiptId
                        JOIN StockReceipts newReceipt ON newReceipt.Id = NEW.StockReceiptId
                        WHERE existing.Id <> NEW.Id
                          AND existing.AccountableFormTypeId = NEW.AccountableFormTypeId
                          AND existingReceipt.SeriesYear = newReceipt.SeriesYear
                          AND existingReceipt.Status <> 9
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;

                CREATE TRIGGER TR_AccountableFormBooklets_NoOverlap_Insert
                BEFORE INSERT ON AccountableFormBooklets
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping accountable form booklet serial range.')
                    WHERE EXISTS (
                        SELECT 1
                        FROM AccountableFormBooklets existing
                        WHERE existing.AccountableFormTypeId = NEW.AccountableFormTypeId
                          AND existing.SeriesYear = NEW.SeriesYear
                          AND existing.Status <> 11
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;

                CREATE TRIGGER TR_AccountableFormBooklets_NoOverlap_Update
                BEFORE UPDATE OF AccountableFormTypeId, SeriesYear, BeginningSerial, EndingSerial, Status ON AccountableFormBooklets
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping accountable form booklet serial range.')
                    WHERE NEW.Status <> 11
                      AND EXISTS (
                        SELECT 1
                        FROM AccountableFormBooklets existing
                        WHERE existing.Id <> NEW.Id
                          AND existing.AccountableFormTypeId = NEW.AccountableFormTypeId
                          AND existing.SeriesYear = NEW.SeriesYear
                          AND existing.Status <> 11
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;

                CREATE TRIGGER TR_AccountabilityRanges_NoOverlap_Insert
                BEFORE INSERT ON AccountabilityRanges
                WHEN NEW.IsActive = 1
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping active accountability range.')
                    WHERE EXISTS (
                        SELECT 1
                        FROM AccountabilityRanges existing
                        WHERE existing.AccountableFormBookletId = NEW.AccountableFormBookletId
                          AND existing.IsActive = 1
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;

                CREATE TRIGGER TR_AccountabilityRanges_NoOverlap_Update
                BEFORE UPDATE OF AccountableFormBookletId, BeginningSerial, EndingSerial, IsActive ON AccountabilityRanges
                WHEN NEW.IsActive = 1
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping active accountability range.')
                    WHERE EXISTS (
                        SELECT 1
                        FROM AccountabilityRanges existing
                        WHERE existing.Id <> NEW.Id
                          AND existing.AccountableFormBookletId = NEW.AccountableFormBookletId
                          AND existing.IsActive = 1
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial
                    );
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS TR_AccountabilityRanges_NoOverlap_Update;
                DROP TRIGGER IF EXISTS TR_AccountabilityRanges_NoOverlap_Insert;
                DROP TRIGGER IF EXISTS TR_AccountableFormBooklets_NoOverlap_Update;
                DROP TRIGGER IF EXISTS TR_AccountableFormBooklets_NoOverlap_Insert;
                DROP TRIGGER IF EXISTS TR_StockReceiptItems_NoOverlap_Update;
                DROP TRIGGER IF EXISTS TR_StockReceiptItems_NoOverlap_Insert;
                """);
        }
    }
}
