using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7SecurityBackupFiscalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAt",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionComments",
                table: "Transfers",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAt",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionComments",
                table: "ReturnTransactions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAt",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionComments",
                table: "Issuances",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "Issuances",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecisionAt",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionComments",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseRequestReason",
                table: "FiscalYears",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CloseRequestedAt",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CloseRequestedByUserId",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloseValidationIssues",
                table: "FiscalYears",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CloseWithDocumentedExceptions",
                table: "FiscalYears",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "FiscalYears",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "FiscalYears",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReopenRequestedAt",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenRequestedByUserId",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReopenedAt",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenedByUserId",
                table: "FiscalYears",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotReference",
                table: "FiscalYears",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowStatus",
                table: "FiscalYears",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousHash",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FiscalYearCarryForwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetYear = table.Column<int>(type: "INTEGER", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearCarryForwards", x => x.Id);
                    table.CheckConstraint("CK_FiscalYearCarryForward_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");
                    table.CheckConstraint("CK_FiscalYearCarryForward_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_FiscalYearCarryForwards_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalYearCarryForwards_Collectors_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalYearCarryForwards_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYearSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningQuantity = table.Column<long>(type: "INTEGER", nullable: false),
                    Additions = table.Column<long>(type: "INTEGER", nullable: false),
                    Usage = table.Column<long>(type: "INTEGER", nullable: false),
                    Exceptions = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingQuantity = table.Column<long>(type: "INTEGER", nullable: false),
                    OutstandingQuantity = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYearSnapshots", x => x.Id);
                    table.CheckConstraint("CK_FiscalYearSnapshot_Quantities", "BeginningQuantity >= 0 AND Additions >= 0 AND Usage >= 0 AND Exceptions >= 0 AND EndingQuantity >= 0 AND OutstandingQuantity >= 0");
                    table.ForeignKey(
                        name: "FK_FiscalYearSnapshots_AccountableFormTypes_AccountableFormTypeId",
                        column: x => x.AccountableFormTypeId,
                        principalTable: "AccountableFormTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FiscalYearSnapshots_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_WorkflowStatus",
                table: "FiscalYears",
                column: "WorkflowStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IntegrityHash",
                table: "AuditLogs",
                column: "IntegrityHash",
                unique: true,
                filter: "IntegrityHash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Module_ActivityType_OccurredAt",
                table: "AuditLogs",
                columns: new[] { "Module", "ActivityType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ReferenceNumber",
                table: "AuditLogs",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCarryForwards_AccountableFormBookletId",
                table: "FiscalYearCarryForwards",
                column: "AccountableFormBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCarryForwards_CollectorId",
                table: "FiscalYearCarryForwards",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCarryForwards_FiscalYearId_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "FiscalYearCarryForwards",
                columns: new[] { "FiscalYearId", "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearCarryForwards_IsActive",
                table: "FiscalYearCarryForwards",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearSnapshots_AccountableFormTypeId",
                table: "FiscalYearSnapshots",
                column: "AccountableFormTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearSnapshots_FiscalYearId_AccountableFormTypeId",
                table: "FiscalYearSnapshots",
                columns: new[] { "FiscalYearId", "AccountableFormTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYearSnapshots_IsActive",
                table: "FiscalYearSnapshots",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalYearCarryForwards");

            migrationBuilder.DropTable(
                name: "FiscalYearSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_FiscalYears_WorkflowStatus",
                table: "FiscalYears");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_IntegrityHash",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Module_ActivityType_OccurredAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ReferenceNumber",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "DecisionComments",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "DecisionComments",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "DecisionComments",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Issuances");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "DecisionComments",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "CloseRequestReason",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "CloseRequestedAt",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "CloseRequestedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "CloseValidationIssues",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "CloseWithDocumentedExceptions",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ReopenRequestedAt",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ReopenRequestedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "ReopenedByUserId",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "SnapshotReference",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "FiscalYears");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Module",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PreviousHash",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "AuditLogs");
        }
    }
}
