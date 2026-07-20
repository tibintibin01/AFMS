using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10IssuanceReservationGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_IssuanceItems_NoReservationOverlap_Insert
                BEFORE INSERT ON IssuanceItems
                WHEN NEW.IsReservationActive = 1
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping active issuance reservation.')
                    WHERE EXISTS (
                        SELECT 1 FROM IssuanceItems existing
                        WHERE existing.AccountableFormBookletId = NEW.AccountableFormBookletId
                          AND existing.IsReservationActive = 1
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial);
                END;

                CREATE TRIGGER TR_IssuanceItems_NoReservationOverlap_Update
                BEFORE UPDATE OF AccountableFormBookletId, BeginningSerial, EndingSerial, IsReservationActive ON IssuanceItems
                WHEN NEW.IsReservationActive = 1
                BEGIN
                    SELECT RAISE(ABORT, 'Overlapping active issuance reservation.')
                    WHERE EXISTS (
                        SELECT 1 FROM IssuanceItems existing
                        WHERE existing.Id <> NEW.Id
                          AND existing.AccountableFormBookletId = NEW.AccountableFormBookletId
                          AND existing.IsReservationActive = 1
                          AND NEW.BeginningSerial <= existing.EndingSerial
                          AND existing.BeginningSerial <= NEW.EndingSerial);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS TR_IssuanceItems_NoReservationOverlap_Update;
                DROP TRIGGER IF EXISTS TR_IssuanceItems_NoReservationOverlap_Insert;
                """);
        }
    }
}
