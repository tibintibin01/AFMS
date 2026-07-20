using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2MasterDataAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountableFormType_DefaultReceipts",
                table: "AccountableFormTypes");

            migrationBuilder.Sql(
                "UPDATE AccountableFormTypes SET DefaultReceiptsPerBooklet = 1 WHERE DefaultReceiptsPerBooklet <= 0");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AppointmentReference",
                table: "Collectors",
                type: "TEXT",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactNumber",
                table: "Collectors",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailAddress",
                table: "Collectors",
                type: "TEXT",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficeOrDepartment",
                table: "Collectors",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Collectors",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                table: "Barangays",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "Barangays",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "Barangays",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NewValues",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldValues",
                table: "AuditLogs",
                type: "TEXT",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "AccountableFormTypes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "AccountableFormTypes",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresIndividualReceiptTracking",
                table: "AccountableFormTypes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SerialNumberLength",
                table: "AccountableFormTypes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumberPrefix",
                table: "AccountableFormTypes",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountableFormType_DefaultReceipts",
                table: "AccountableFormTypes",
                sql: "DefaultReceiptsPerBooklet > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountableFormType_SerialLength",
                table: "AccountableFormTypes",
                sql: "SerialNumberLength IS NULL OR SerialNumberLength > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_IsActive",
                table: "RolePermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_Permission",
                table: "RolePermissions",
                columns: new[] { "RoleId", "Permission" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountableFormType_DefaultReceipts",
                table: "AccountableFormTypes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountableFormType_SerialLength",
                table: "AccountableFormTypes");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AppointmentReference",
                table: "Collectors");

            migrationBuilder.DropColumn(
                name: "ContactNumber",
                table: "Collectors");

            migrationBuilder.DropColumn(
                name: "EmailAddress",
                table: "Collectors");

            migrationBuilder.DropColumn(
                name: "OfficeOrDepartment",
                table: "Collectors");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Collectors");

            migrationBuilder.DropColumn(
                name: "Municipality",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "Province",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "Barangays");

            migrationBuilder.DropColumn(
                name: "NewValues",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OldValues",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "AccountableFormTypes");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "AccountableFormTypes");

            migrationBuilder.DropColumn(
                name: "RequiresIndividualReceiptTracking",
                table: "AccountableFormTypes");

            migrationBuilder.DropColumn(
                name: "SerialNumberLength",
                table: "AccountableFormTypes");

            migrationBuilder.DropColumn(
                name: "SerialNumberPrefix",
                table: "AccountableFormTypes");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 100,
                oldCollation: "NOCASE");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountableFormType_DefaultReceipts",
                table: "AccountableFormTypes",
                sql: "DefaultReceiptsPerBooklet >= 0");
        }
    }
}
