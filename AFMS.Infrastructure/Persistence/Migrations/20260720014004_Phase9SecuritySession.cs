using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9SecuritySession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecurityVersion",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_FailedLoginCount",
                table: "Users",
                sql: "FailedLoginCount >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_User_SecurityVersion",
                table: "Users",
                sql: "SecurityVersion > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_User_FailedLoginCount",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_User_SecurityVersion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityVersion",
                table: "Users");
        }
    }
}
