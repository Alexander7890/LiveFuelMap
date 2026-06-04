using System;
using LiveFuelMap.DAL.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiveFuelMapDbContext))]
    [Migration("20260601103000_AddSubscriptionSchedule")]
    public partial class AddSubscriptionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "subscriptions",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "subscriptions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSentAt",
                table: "subscriptions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "SendTime",
                table: "subscriptions",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(9, 0, 0));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "subscriptions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_Frequency_IsActive_SendTime",
                table: "subscriptions",
                columns: new[] { "Frequency", "IsActive", "SendTime" });

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_UserId_FuelId_City",
                table: "subscriptions",
                columns: new[] { "UserId", "FuelId", "City" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_UserId_FuelId_City_Frequency",
                table: "subscriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_UserId_FuelId_City_Frequency",
                table: "subscriptions",
                columns: new[] { "UserId", "FuelId", "City", "Frequency" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_Frequency_IsActive_SendTime",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_UserId_FuelId_City",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "SendTime",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "subscriptions");
        }
    }
}
