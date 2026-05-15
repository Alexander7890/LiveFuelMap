using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "application_logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Level = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Message = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Exception = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_logs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "data_sources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastSuccessAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_sources", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fuels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuels", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalizedKey = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Latitude = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EmailConfirmationTokenHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordResetTokenHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordResetTokenExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "parser_runs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordsFound = table.Column<int>(type: "int", nullable: false),
                    RecordsSaved = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Duration = table.Column<TimeSpan>(type: "time(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parser_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_parser_runs_data_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "data_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fuel_prices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    FuelId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Popularity = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    IsManual = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fuel_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_fuel_prices_data_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "data_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_fuel_prices_fuels_FuelId",
                        column: x => x.FuelId,
                        principalTable: "fuels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fuel_prices_stations_StationId",
                        column: x => x.StationId,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "api_tokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TokenHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Scopes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_tokens_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<int>(type: "int", nullable: false),
                    FuelId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comments_fuels_FuelId",
                        column: x => x.FuelId,
                        principalTable: "fuels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_comments_stations_StationId",
                        column: x => x.StationId,
                        principalTable: "stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comments_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FuelId = table.Column<int>(type: "int", nullable: false),
                    City = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Frequency = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscriptions_fuels_FuelId",
                        column: x => x.FuelId,
                        principalTable: "fuels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "data_sources",
                columns: new[] { "Id", "Enabled", "LastSuccessAt", "Name", "Type", "Url" },
                values: new object[,]
                {
                    { 1, true, null, "Minfin Kharkiv", "Html", "https://index.minfin.com.ua/ua/markets/fuel/reg/harkovskaya/" },
                    { 2, false, null, "Open Fuel JSON", "Json", "https://example.com/fuel-prices.json" }
                });

            migrationBuilder.InsertData(
                table: "fuels",
                columns: new[] { "Id", "Code", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "a95plus", "? 95+", 1 },
                    { 2, "a95", "? 95", 2 },
                    { 3, "a92", "? 92", 3 },
                    { 4, "diesel", "??", 4 },
                    { 5, "gas", "???", 5 }
                });

            migrationBuilder.InsertData(
                table: "stations",
                columns: new[] { "Id", "Address", "City", "CreatedAt", "ImageUrl", "IsActive", "Latitude", "Longitude", "Name", "NormalizedKey", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "???????? ?????? ???????, 142A", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.966850m, 36.317010m, "AMIC", "amic", null },
                    { 2, "?????? ???????? ?????????, 85", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 50.012210m, 36.255580m, "Marshal", "marshal", null },
                    { 3, "?????? ???????????, 98?", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 50.005090m, 36.218900m, "Ovis", "ovis", null },
                    { 4, "????????????? ????????, 223", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.884170m, 36.291900m, "Rodnik", "rodnik", null },
                    { 5, "?????? ?????????", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.947570m, 36.186600m, "SUN OIL", "sun-oil", null },
                    { 6, "???????? ????? ??????????", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.945000m, 36.313130m, "Shell", "shell", null },
                    { 7, "????????????? ????????, 127?", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.964610m, 36.259940m, "U.GO", "ugo", null },
                    { 8, "?????? ????????, 41", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.996090m, 36.250020m, "WOG", "wog", null },
                    { 9, "???????? ???????", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.945400m, 36.332170m, "?????", "avias", null },
                    { 10, "?????? ??????????????, 2?", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 50.021000m, 36.319070m, "????-?????", "brsm-nafta", null },
                    { 11, "?????? ???????????, 44", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.997310m, 36.227570m, "????", "okko", null },
                    { 12, "?????? ????????? ???????", "??????", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "default-station.jpg", true, 49.989690m, 36.287720m, "????????", "ukrnafta", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_CreatedByUserId",
                table: "api_tokens",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_RevokedAt",
                table: "api_tokens",
                column: "RevokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_TokenHash",
                table: "api_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_logs_Category_CreatedAt",
                table: "application_logs",
                columns: new[] { "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_FuelId",
                table: "comments",
                column: "FuelId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_StationId",
                table: "comments",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_comments_UserId",
                table: "comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_data_sources_Name",
                table: "data_sources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fuel_prices_FuelId_Date",
                table: "fuel_prices",
                columns: new[] { "FuelId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_fuel_prices_SourceId",
                table: "fuel_prices",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_fuel_prices_StationId_FuelId_Date",
                table: "fuel_prices",
                columns: new[] { "StationId", "FuelId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fuels_Code",
                table: "fuels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fuels_Name",
                table: "fuels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_parser_runs_SourceId_StartedAt",
                table: "parser_runs",
                columns: new[] { "SourceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stations_City",
                table: "stations",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_stations_NormalizedKey_City",
                table: "stations",
                columns: new[] { "NormalizedKey", "City" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_FuelId",
                table: "subscriptions",
                column: "FuelId");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_UserId_FuelId_City_Frequency",
                table: "subscriptions",
                columns: new[] { "UserId", "FuelId", "City", "Frequency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_tokens");

            migrationBuilder.DropTable(
                name: "application_logs");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "fuel_prices");

            migrationBuilder.DropTable(
                name: "parser_runs");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "stations");

            migrationBuilder.DropTable(
                name: "data_sources");

            migrationBuilder.DropTable(
                name: "fuels");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
