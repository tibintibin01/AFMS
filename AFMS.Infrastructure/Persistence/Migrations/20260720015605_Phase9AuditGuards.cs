using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AuditGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_AuditLogs_AppendOnly_Update
                BEFORE UPDATE ON AuditLogs
                BEGIN
                    SELECT RAISE(ABORT, 'Audit records are append-only and cannot be updated.');
                END;

                CREATE TRIGGER TR_AuditLogs_AppendOnly_Delete
                BEFORE DELETE ON AuditLogs
                BEGIN
                    SELECT RAISE(ABORT, 'Audit records are append-only and cannot be deleted.');
                END;

                CREATE TRIGGER TR_AuditLogs_RequireIntegrityHash
                BEFORE INSERT ON AuditLogs
                WHEN NEW.IntegrityHash IS NULL OR length(trim(NEW.IntegrityHash)) <> 64
                BEGIN
                    SELECT RAISE(ABORT, 'Audit records require a valid integrity hash.');
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS TR_AuditLogs_RequireIntegrityHash;
                DROP TRIGGER IF EXISTS TR_AuditLogs_AppendOnly_Delete;
                DROP TRIGGER IF EXISTS TR_AuditLogs_AppendOnly_Update;
                """);
        }
    }
}
