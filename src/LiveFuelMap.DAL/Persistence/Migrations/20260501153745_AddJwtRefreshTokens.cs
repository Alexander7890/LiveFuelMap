using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJwtRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiresAt",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenHash",
                table: "users",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenRevokedAt",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_users_RefreshTokenHash",
                table: "users",
                column: "RefreshTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_RefreshTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenHash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenRevokedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "users");
        }
    }
}
