using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5UsageTransfersReturnsExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FormExceptions_AccountableFormBookletId_SerialNumber_ExceptionType",
                table: "FormExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FormException_Serial",
                table: "FormExceptions");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountableFormTypeId",
                table: "UsageReports",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "AmountCollected",
                table: "UsageReports",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EncodedByUserId",
                table: "UsageReports",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "UsageReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "UsageReports",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReportingDate",
                table: "UsageReports",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "UsageReports",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "UsageReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReportNumber",
                table: "UsageReports",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GapExplanation",
                table: "UsageRanges",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "UsageRanges",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BarangayId",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EncodedByUserId",
                table: "Transfers",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "Transfers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Transfers",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "Transfers",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReleasedBy",
                table: "Transfers",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "Transfers",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "Transfers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "ReturnTransactions",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Disposition",
                table: "ReturnTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "EncodedByUserId",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalIssuanceId",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalCondition",
                table: "ReturnTransactions",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "ReturnTransactions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedBy",
                table: "ReturnTransactions",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "ReturnTransactions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionTaken",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateDiscovered",
                table: "FormExceptions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<Guid>(
                name: "EncodedByUserId",
                table: "FormExceptions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "EndingSerial",
                table: "FormExceptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Quantity",
                table: "FormExceptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionStatus",
                table: "FormExceptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "FormExceptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportingDocumentReference",
                table: "FormExceptions",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountabilityRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentCollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentBarangayId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StorageLocation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SourceIssuanceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastTransactionType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LastTransactionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousCollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PreviousBarangayId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PreviousStorageLocation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountabilityRanges", x => x.Id);
                    table.CheckConstraint("CK_AccountabilityRange_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_AccountabilityRanges_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountabilityRanges_Barangays_CurrentBarangayId",
                        column: x => x.CurrentBarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountabilityRanges_Collectors_CurrentCollectorId",
                        column: x => x.CurrentCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountabilityRanges_Issuances_SourceIssuanceId",
                        column: x => x.SourceIssuanceId,
                        principalTable: "Issuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransactionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelatedTransactionType = table.Column<int>(type: "INTEGER", nullable: false),
                    RelatedTransactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionAttachments", x => x.Id);
                    table.CheckConstraint("CK_TransactionAttachment_Size", "FileSize > 0");
                    table.ForeignKey(
                        name: "FK_TransactionAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageReports_AccountableFormTypeId",
                table: "UsageReports",
                column: "AccountableFormTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageReports_EncodedByUserId",
                table: "UsageReports",
                column: "EncodedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageRange_Quantity",
                table: "UsageRanges",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ApprovedByUserId",
                table: "Transfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_BarangayId",
                table: "Transfers",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_EncodedByUserId",
                table: "Transfers",
                column: "EncodedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transfer_Quantity",
                table: "Transfers",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_ApprovedByUserId",
                table: "ReturnTransactions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_EncodedByUserId",
                table: "ReturnTransactions",
                column: "EncodedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_OriginalIssuanceId",
                table: "ReturnTransactions",
                column: "OriginalIssuanceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReturnTransaction_Quantity",
                table: "ReturnTransactions",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_AccountableFormBookletId_SerialNumber_EndingSerial_ExceptionType",
                table: "FormExceptions",
                columns: new[] { "AccountableFormBookletId", "SerialNumber", "EndingSerial", "ExceptionType" });

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_ApprovedByUserId",
                table: "FormExceptions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_EncodedByUserId",
                table: "FormExceptions",
                column: "EncodedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_ReferenceNumber",
                table: "FormExceptions",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FormException_Quantity",
                table: "FormExceptions",
                sql: "Quantity > 0 AND Quantity = (EndingSerial - SerialNumber + 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FormException_Serial",
                table: "FormExceptions",
                sql: "SerialNumber >= 0 AND EndingSerial >= SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "AccountabilityRanges",
                columns: new[] { "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_CurrentBarangayId",
                table: "AccountabilityRanges",
                column: "CurrentBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_CurrentCollectorId_Status_IsActive",
                table: "AccountabilityRanges",
                columns: new[] { "CurrentCollectorId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_IsActive",
                table: "AccountabilityRanges",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_LastTransactionType_LastTransactionId",
                table: "AccountabilityRanges",
                columns: new[] { "LastTransactionType", "LastTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountabilityRanges_SourceIssuanceId",
                table: "AccountabilityRanges",
                column: "SourceIssuanceId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_IsActive",
                table: "TransactionAttachments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_RelatedTransactionType_RelatedTransactionId",
                table: "TransactionAttachments",
                columns: new[] { "RelatedTransactionType", "RelatedTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_StoredFileName",
                table: "TransactionAttachments",
                column: "StoredFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_UploadedByUserId",
                table: "TransactionAttachments",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormExceptions_Users_ApprovedByUserId",
                table: "FormExceptions",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FormExceptions_Users_EncodedByUserId",
                table: "FormExceptions",
                column: "EncodedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnTransactions_Issuances_OriginalIssuanceId",
                table: "ReturnTransactions",
                column: "OriginalIssuanceId",
                principalTable: "Issuances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnTransactions_Users_ApprovedByUserId",
                table: "ReturnTransactions",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReturnTransactions_Users_EncodedByUserId",
                table: "ReturnTransactions",
                column: "EncodedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Barangays_BarangayId",
                table: "Transfers",
                column: "BarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Users_ApprovedByUserId",
                table: "Transfers",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transfers_Users_EncodedByUserId",
                table: "Transfers",
                column: "EncodedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageReports_AccountableFormTypes_AccountableFormTypeId",
                table: "UsageReports",
                column: "AccountableFormTypeId",
                principalTable: "AccountableFormTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageReports_Users_EncodedByUserId",
                table: "UsageReports",
                column: "EncodedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormExceptions_Users_ApprovedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_FormExceptions_Users_EncodedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnTransactions_Issuances_OriginalIssuanceId",
                table: "ReturnTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnTransactions_Users_ApprovedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReturnTransactions_Users_EncodedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Barangays_BarangayId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Users_ApprovedByUserId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_Transfers_Users_EncodedByUserId",
                table: "Transfers");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageReports_AccountableFormTypes_AccountableFormTypeId",
                table: "UsageReports");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageReports_Users_EncodedByUserId",
                table: "UsageReports");

            migrationBuilder.DropTable(
                name: "AccountabilityRanges");

            migrationBuilder.DropTable(
                name: "TransactionAttachments");

            migrationBuilder.DropIndex(
                name: "IX_UsageReports_AccountableFormTypeId",
                table: "UsageReports");

            migrationBuilder.DropIndex(
                name: "IX_UsageReports_EncodedByUserId",
                table: "UsageReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageRange_Quantity",
                table: "UsageRanges");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_ApprovedByUserId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_BarangayId",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_Transfers_EncodedByUserId",
                table: "Transfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transfer_Quantity",
                table: "Transfers");

            migrationBuilder.DropIndex(
                name: "IX_ReturnTransactions_ApprovedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ReturnTransactions_EncodedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropIndex(
                name: "IX_ReturnTransactions_OriginalIssuanceId",
                table: "ReturnTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReturnTransaction_Quantity",
                table: "ReturnTransactions");

            migrationBuilder.DropIndex(
                name: "IX_FormExceptions_AccountableFormBookletId_SerialNumber_EndingSerial_ExceptionType",
                table: "FormExceptions");

            migrationBuilder.DropIndex(
                name: "IX_FormExceptions_ApprovedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropIndex(
                name: "IX_FormExceptions_EncodedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropIndex(
                name: "IX_FormExceptions_ReferenceNumber",
                table: "FormExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FormException_Quantity",
                table: "FormExceptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FormException_Serial",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "AccountableFormTypeId",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "AmountCollected",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "EncodedByUserId",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "ReportingDate",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "SourceReportNumber",
                table: "UsageReports");

            migrationBuilder.DropColumn(
                name: "GapExplanation",
                table: "UsageRanges");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "UsageRanges");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "BarangayId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "EncodedByUserId",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ReleasedBy",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "Disposition",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "EncodedByUserId",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalIssuanceId",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "PhysicalCondition",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "ReceivedBy",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "ReturnTransactions");

            migrationBuilder.DropColumn(
                name: "ActionTaken",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "DateDiscovered",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "EncodedByUserId",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "EndingSerial",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ResolutionStatus",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "FormExceptions");

            migrationBuilder.DropColumn(
                name: "SupportingDocumentReference",
                table: "FormExceptions");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "ReturnTransactions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1000);

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_AccountableFormBookletId_SerialNumber_ExceptionType",
                table: "FormExceptions",
                columns: new[] { "AccountableFormBookletId", "SerialNumber", "ExceptionType" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FormException_Serial",
                table: "FormExceptions",
                sql: "SerialNumber >= 0");
        }
    }
}
