using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AFMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AdministratorGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TRIGGER TR_Users_KeepAdministrator_Update
                BEFORE UPDATE OF IsActive ON Users
                WHEN OLD.IsActive = 1 AND NEW.IsActive = 0
                BEGIN
                    SELECT RAISE(ABORT, 'AFMS must retain at least one active Administrator account.')
                    WHERE EXISTS (
                        SELECT 1 FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = OLD.Id AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE)
                      AND NOT EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId
                        WHERE u.Id <> OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE);
                END;

                CREATE TRIGGER TR_Users_KeepAdministrator_Delete
                BEFORE DELETE ON Users
                BEGIN
                    SELECT RAISE(ABORT, 'AFMS must retain at least one active Administrator account.')
                    WHERE OLD.IsActive = 1
                      AND EXISTS (
                        SELECT 1 FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.UserId = OLD.Id AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE)
                      AND NOT EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId
                        WHERE u.Id <> OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE);
                END;

                CREATE TRIGGER TR_UserRoles_KeepAdministrator_Update
                BEFORE UPDATE OF IsActive, RoleId ON UserRoles
                WHEN OLD.IsActive = 1
                BEGIN
                    SELECT RAISE(ABORT, 'AFMS must retain at least one active Administrator account.')
                    WHERE EXISTS (SELECT 1 FROM Users u WHERE u.Id = OLD.UserId AND u.IsActive = 1)
                      AND EXISTS (SELECT 1 FROM Roles r WHERE r.Id = OLD.RoleId AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE)
                      AND (NEW.IsActive = 0 OR NOT EXISTS (
                          SELECT 1 FROM Roles r WHERE r.Id = NEW.RoleId AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE))
                      AND NOT EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.Id <> OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE);
                END;

                CREATE TRIGGER TR_UserRoles_KeepAdministrator_Delete
                BEFORE DELETE ON UserRoles
                BEGIN
                    SELECT RAISE(ABORT, 'AFMS must retain at least one active Administrator account.')
                    WHERE OLD.IsActive = 1
                      AND EXISTS (SELECT 1 FROM Users u WHERE u.Id = OLD.UserId AND u.IsActive = 1)
                      AND EXISTS (SELECT 1 FROM Roles r WHERE r.Id = OLD.RoleId AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE)
                      AND NOT EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id JOIN Roles r ON r.Id = ur.RoleId
                        WHERE ur.Id <> OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1 AND r.IsActive = 1 AND r.Name = 'Administrator' COLLATE NOCASE);
                END;

                CREATE TRIGGER TR_Roles_KeepAdministrator_Update
                BEFORE UPDATE OF IsActive, Name ON Roles
                WHEN OLD.IsActive = 1 AND OLD.Name = 'Administrator' COLLATE NOCASE
                BEGIN
                    SELECT RAISE(ABORT, 'The active Administrator role cannot be disabled or renamed.')
                    WHERE (NEW.IsActive = 0 OR NEW.Name <> 'Administrator' COLLATE NOCASE)
                      AND EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id
                        WHERE ur.RoleId = OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1);
                END;

                CREATE TRIGGER TR_Roles_KeepAdministrator_Delete
                BEFORE DELETE ON Roles
                WHEN OLD.IsActive = 1 AND OLD.Name = 'Administrator' COLLATE NOCASE
                BEGIN
                    SELECT RAISE(ABORT, 'The active Administrator role cannot be deleted.')
                    WHERE EXISTS (
                        SELECT 1 FROM Users u JOIN UserRoles ur ON ur.UserId = u.Id
                        WHERE ur.RoleId = OLD.Id AND u.IsActive = 1 AND ur.IsActive = 1);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS TR_Roles_KeepAdministrator_Delete;
                DROP TRIGGER IF EXISTS TR_Roles_KeepAdministrator_Update;
                DROP TRIGGER IF EXISTS TR_UserRoles_KeepAdministrator_Delete;
                DROP TRIGGER IF EXISTS TR_UserRoles_KeepAdministrator_Update;
                DROP TRIGGER IF EXISTS TR_Users_KeepAdministrator_Delete;
                DROP TRIGGER IF EXISTS TR_Users_KeepAdministrator_Update;
                """);
        }
    }
}
