using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Activity = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TracksSerials = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReorderLevel = table.Column<long>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.CheckConstraint("CK_Item_ReorderLevel", "ReorderLevel >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Office = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    LastNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceSequences", x => x.Id);
                    table.CheckConstraint("CK_ReferenceSequence_LastNumber", "LastNumber >= 0");
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedSignInCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedOutUntil = table.Column<long>(type: "INTEGER", nullable: true),
                    LastSignInAt = table.Column<long>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_User_FailedSignInCount", "FailedSignInCount >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Releases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RecipientId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ReleasedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ReceivedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Releases_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Releases_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DocumentNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReturnedByRecipientId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReceivedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIns", x => x.Id);
                    table.CheckConstraint("CK_StockIn_ReturnNeedsRecipient", "Source <> 3 OR ReturnedByRecipientId IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_StockIns_Recipients_ReturnedByRecipientId",
                        column: x => x.ReturnedByRecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockIns_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    SerialFrom = table.Column<long>(type: "INTEGER", nullable: true),
                    SerialTo = table.Column<long>(type: "INTEGER", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseLines", x => x.Id);
                    table.CheckConstraint("CK_ReleaseLine_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_ReleaseLine_Serials", "(SerialFrom IS NULL AND SerialTo IS NULL) OR (SerialFrom IS NOT NULL AND SerialTo IS NOT NULL AND SerialFrom >= 0 AND SerialTo >= SerialFrom)");
                    table.ForeignKey(
                        name: "FK_ReleaseLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReleaseLines_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockInLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockInId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    SerialFrom = table.Column<long>(type: "INTEGER", nullable: true),
                    SerialTo = table.Column<long>(type: "INTEGER", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockInLines", x => x.Id);
                    table.CheckConstraint("CK_StockInLine_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_StockInLine_Serials", "(SerialFrom IS NULL AND SerialTo IS NULL) OR (SerialFrom IS NOT NULL AND SerialTo IS NOT NULL AND SerialFrom >= 0 AND SerialTo >= SerialFrom)");
                    table.ForeignKey(
                        name: "FK_StockInLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockInLines_StockIns_StockInId",
                        column: x => x.StockInId,
                        principalTable: "StockIns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Activity",
                table: "AuditLogs",
                column: "Activity");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ReferenceNumber",
                table: "AuditLogs",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Code",
                table: "Items",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_IsActive",
                table: "Items",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                table: "Items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_Code",
                table: "Recipients",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_IsActive",
                table: "Recipients",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_Name",
                table: "Recipients",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceSequences_Prefix_Year",
                table: "ReferenceSequences",
                columns: new[] { "Prefix", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseLines_ItemId_SerialFrom_SerialTo",
                table: "ReleaseLines",
                columns: new[] { "ItemId", "SerialFrom", "SerialTo" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseLines_ReleaseId",
                table: "ReleaseLines",
                column: "ReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_RecipientId_Status",
                table: "Releases",
                columns: new[] { "RecipientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Releases_RecordedByUserId",
                table: "Releases",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReferenceNumber",
                table: "Releases",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleaseDate",
                table: "Releases",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_Status",
                table: "Releases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_ItemId_SerialFrom_SerialTo",
                table: "StockInLines",
                columns: new[] { "ItemId", "SerialFrom", "SerialTo" });

            migrationBuilder.CreateIndex(
                name: "IX_StockInLines_StockInId",
                table: "StockInLines",
                column: "StockInId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_EntryDate",
                table: "StockIns",
                column: "EntryDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_RecordedByUserId",
                table: "StockIns",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_ReferenceNumber",
                table: "StockIns",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_ReturnedByRecipientId",
                table: "StockIns",
                column: "ReturnedByRecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_Source_ReturnedByRecipientId",
                table: "StockIns",
                columns: new[] { "Source", "ReturnedByRecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockIns_Status",
                table: "StockIns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ReferenceSequences");

            migrationBuilder.DropTable(
                name: "ReleaseLines");

            migrationBuilder.DropTable(
                name: "StockInLines");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Releases");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "StockIns");

            migrationBuilder.DropTable(
                name: "Recipients");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
