using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiveFuelMapDbContext))]
    [Migration("20260529104500_RepairGoogleNicknameColumns")]
    public partial class RepairGoogleNicknameColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @googleNameExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND COLUMN_NAME = 'GoogleName'
                );
                SET @sql = IF(
                    @googleNameExists = 0,
                    'ALTER TABLE `users` ADD COLUMN `GoogleName` varchar(100) NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @requiresNicknameSetupExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND COLUMN_NAME = 'RequiresNicknameSetup'
                );
                SET @sql = IF(
                    @requiresNicknameSetupExists = 0,
                    'ALTER TABLE `users` ADD COLUMN `RequiresNicknameSetup` tinyint(1) NOT NULL DEFAULT 0',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @requiresNicknameSetupIndexExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND INDEX_NAME = 'IX_users_RequiresNicknameSetup'
                );
                SET @sql = IF(
                    @requiresNicknameSetupIndexExists = 0,
                    'CREATE INDEX `IX_users_RequiresNicknameSetup` ON `users` (`RequiresNicknameSetup`)',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @requiresNicknameSetupIndexExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND INDEX_NAME = 'IX_users_RequiresNicknameSetup'
                );
                SET @sql = IF(
                    @requiresNicknameSetupIndexExists > 0,
                    'DROP INDEX `IX_users_RequiresNicknameSetup` ON `users`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @requiresNicknameSetupExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND COLUMN_NAME = 'RequiresNicknameSetup'
                );
                SET @sql = IF(
                    @requiresNicknameSetupExists > 0,
                    'ALTER TABLE `users` DROP COLUMN `RequiresNicknameSetup`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @googleNameExists = (
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'users'
                      AND COLUMN_NAME = 'GoogleName'
                );
                SET @sql = IF(
                    @googleNameExists > 0,
                    'ALTER TABLE `users` DROP COLUMN `GoogleName`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
