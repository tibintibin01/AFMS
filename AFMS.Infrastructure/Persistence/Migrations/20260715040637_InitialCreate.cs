using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountableFormTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FormNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultReceiptsPerBooklet = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountableFormTypes", x => x.Id);
                    table.CheckConstraint("CK_AccountableFormType_DefaultReceipts", "DefaultReceiptsPerBooklet >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Barangays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Barangays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsClosed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalYears", x => x.Id);
                    table.CheckConstraint("CK_FiscalYear_Dates", "EndDate >= StartDate");
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
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
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReceivedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: true),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceipts_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActivityType = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", nullable: true),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MachineName = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Collectors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BarangayId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collectors_Barangays_BarangayId",
                        column: x => x.BarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Collectors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockReceiptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptItems", x => x.Id);
                    table.CheckConstraint("CK_StockReceiptItem_Quantity", "Quantity > 0");
                    table.CheckConstraint("CK_StockReceiptItem_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_AccountableFormTypes_AccountableFormTypeId",
                        column: x => x.AccountableFormTypeId,
                        principalTable: "AccountableFormTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptItems_StockReceipts_StockReceiptId",
                        column: x => x.StockReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Issuances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IssuedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CollectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issuances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issuances_Collectors_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Issuances_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Issuances_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CollectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    SubmittedDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageReports", x => x.Id);
                    table.CheckConstraint("CK_UsageReport_Period", "PeriodEnd >= PeriodStart");
                    table.ForeignKey(
                        name: "FK_UsageReports_Collectors_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountableFormBooklets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StockReceiptItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormTypeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentCollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountableFormBooklets", x => x.Id);
                    table.CheckConstraint("CK_AccountableFormBooklet_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_AccountableFormBooklets_AccountableFormTypes_AccountableFormTypeId",
                        column: x => x.AccountableFormTypeId,
                        principalTable: "AccountableFormTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountableFormBooklets_Collectors_CurrentCollectorId",
                        column: x => x.CurrentCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountableFormBooklets_StockReceiptItems_StockReceiptItemId",
                        column: x => x.StockReceiptItemId,
                        principalTable: "StockReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FormExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SerialNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    ExceptionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ReportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FormExceptions", x => x.Id);
                    table.CheckConstraint("CK_FormException_Serial", "SerialNumber >= 0");
                    table.ForeignKey(
                        name: "FK_FormExceptions_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FormExceptions_Collectors_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IssuanceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IssuanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuanceItems", x => x.Id);
                    table.CheckConstraint("CK_IssuanceItem_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_IssuanceItems_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IssuanceItems_Issuances_IssuanceId",
                        column: x => x.IssuanceId,
                        principalTable: "Issuances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReturnDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CollectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnTransactions", x => x.Id);
                    table.CheckConstraint("CK_ReturnTransaction_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_ReturnTransactions_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnTransactions_Collectors_CollectorId",
                        column: x => x.CollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementType = table.Column<int>(type: "INTEGER", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    FromCollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToCollectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReferenceType = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovement_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_StockMovements_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Collectors_FromCollectorId",
                        column: x => x.FromCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Collectors_ToCollectorId",
                        column: x => x.ToCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromCollectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToCollectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfer_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_Transfers_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Collectors_FromCollectorId",
                        column: x => x.FromCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Collectors_ToCollectorId",
                        column: x => x.ToCollectorId,
                        principalTable: "Collectors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageRanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsageReportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountableFormBookletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeginningSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    EndingSerial = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRanges", x => x.Id);
                    table.CheckConstraint("CK_UsageRange_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
                    table.ForeignKey(
                        name: "FK_UsageRanges_AccountableFormBooklets_AccountableFormBookletId",
                        column: x => x.AccountableFormBookletId,
                        principalTable: "AccountableFormBooklets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageRanges_UsageReports_UsageReportId",
                        column: x => x.UsageReportId,
                        principalTable: "UsageReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_AccountableFormTypeId_BeginningSerial_EndingSerial",
                table: "AccountableFormBooklets",
                columns: new[] { "AccountableFormTypeId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_CurrentCollectorId",
                table: "AccountableFormBooklets",
                column: "CurrentCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_IsActive",
                table: "AccountableFormBooklets",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_Status_CurrentCollectorId",
                table: "AccountableFormBooklets",
                columns: new[] { "Status", "CurrentCollectorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormBooklets_StockReceiptItemId",
                table: "AccountableFormBooklets",
                column: "StockReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormTypes_Code",
                table: "AccountableFormTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountableFormTypes_IsActive",
                table: "AccountableFormTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IsActive",
                table: "AuditLogs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAt",
                table: "AuditLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_Code",
                table: "Barangays",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_IsActive",
                table: "Barangays",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Barangays_Name",
                table: "Barangays",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collectors_BarangayId",
                table: "Collectors",
                column: "BarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Collectors_Code",
                table: "Collectors",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collectors_EmployeeNumber",
                table: "Collectors",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collectors_IsActive",
                table: "Collectors",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Collectors_UserId",
                table: "Collectors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_IsActive",
                table: "FiscalYears",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_Year",
                table: "FiscalYears",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_AccountableFormBookletId_SerialNumber_ExceptionType",
                table: "FormExceptions",
                columns: new[] { "AccountableFormBookletId", "SerialNumber", "ExceptionType" });

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_CollectorId",
                table: "FormExceptions",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_FormExceptions_IsActive",
                table: "FormExceptions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_AccountableFormBookletId",
                table: "IssuanceItems",
                column: "AccountableFormBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_IsActive",
                table: "IssuanceItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_IssuanceItems_IssuanceId_AccountableFormBookletId",
                table: "IssuanceItems",
                columns: new[] { "IssuanceId", "AccountableFormBookletId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_ApprovedByUserId",
                table: "Issuances",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_CollectorId_IssuedDate_Status",
                table: "Issuances",
                columns: new[] { "CollectorId", "IssuedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_IsActive",
                table: "Issuances",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_IssuedByUserId",
                table: "Issuances",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Issuances_ReferenceNumber",
                table: "Issuances",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_AccountableFormBookletId",
                table: "ReturnTransactions",
                column: "AccountableFormBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_CollectorId",
                table: "ReturnTransactions",
                column: "CollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_IsActive",
                table: "ReturnTransactions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_ReferenceNumber",
                table: "ReturnTransactions",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReturnTransactions_ReturnDate_Status",
                table: "ReturnTransactions",
                columns: new[] { "ReturnDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_IsActive",
                table: "Roles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_AccountableFormBookletId_OccurredAt",
                table: "StockMovements",
                columns: new[] { "AccountableFormBookletId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_FromCollectorId",
                table: "StockMovements",
                column: "FromCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_IsActive",
                table: "StockMovements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PerformedByUserId",
                table: "StockMovements",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType_ReferenceId",
                table: "StockMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ToCollectorId",
                table: "StockMovements",
                column: "ToCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_AccountableFormTypeId_BeginningSerial_EndingSerial",
                table: "StockReceiptItems",
                columns: new[] { "AccountableFormTypeId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_IsActive",
                table: "StockReceiptItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptItems_StockReceiptId",
                table: "StockReceiptItems",
                column: "StockReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_FiscalYearId",
                table: "StockReceipts",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_IsActive",
                table: "StockReceipts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ReceivedDate_Status",
                table: "StockReceipts",
                columns: new[] { "ReceivedDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ReferenceNumber",
                table: "StockReceipts",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_IsActive",
                table: "SystemSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_AccountableFormBookletId",
                table: "Transfers",
                column: "AccountableFormBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_FromCollectorId",
                table: "Transfers",
                column: "FromCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_IsActive",
                table: "Transfers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ReferenceNumber",
                table: "Transfers",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_ToCollectorId",
                table: "Transfers",
                column: "ToCollectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TransferDate_Status",
                table: "Transfers",
                columns: new[] { "TransferDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRanges_AccountableFormBookletId",
                table: "UsageRanges",
                column: "AccountableFormBookletId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRanges_IsActive",
                table: "UsageRanges",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UsageRanges_UsageReportId_AccountableFormBookletId_BeginningSerial_EndingSerial",
                table: "UsageRanges",
                columns: new[] { "UsageReportId", "AccountableFormBookletId", "BeginningSerial", "EndingSerial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageReports_CollectorId_PeriodStart_PeriodEnd",
                table: "UsageReports",
                columns: new[] { "CollectorId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageReports_IsActive",
                table: "UsageReports",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UsageReports_ReferenceNumber",
                table: "UsageReports",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_IsActive",
                table: "UserRoles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
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
                name: "FormExceptions");

            migrationBuilder.DropTable(
                name: "IssuanceItems");

            migrationBuilder.DropTable(
                name: "ReturnTransactions");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "Transfers");

            migrationBuilder.DropTable(
                name: "UsageRanges");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Issuances");

            migrationBuilder.DropTable(
                name: "AccountableFormBooklets");

            migrationBuilder.DropTable(
                name: "UsageReports");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "StockReceiptItems");

            migrationBuilder.DropTable(
                name: "Collectors");

            migrationBuilder.DropTable(
                name: "AccountableFormTypes");

            migrationBuilder.DropTable(
                name: "StockReceipts");

            migrationBuilder.DropTable(
                name: "Barangays");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "FiscalYears");
        }
    }
}
