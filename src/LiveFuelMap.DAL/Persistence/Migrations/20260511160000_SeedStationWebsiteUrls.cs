using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(LiveFuelMapDbContext))]
    [Migration("20260511160000_SeedStationWebsiteUrls")]
    public partial class SeedStationWebsiteUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SetWebsite(migrationBuilder, "amic", "https://amicenergy.com.ua/");
            SetWebsite(migrationBuilder, "marshal", "https://www.azski.com.ua/");
            SetWebsite(migrationBuilder, "ovis", "https://ovis.ua/");
            SetWebsite(migrationBuilder, "shell", "https://www.shell.ua/");
            SetWebsite(migrationBuilder, "ugo", "https://ugo.ua/");
            SetWebsite(migrationBuilder, "wog", "https://wog.ua/");
            SetWebsite(migrationBuilder, "avias", "https://avias.ua/");
            SetWebsite(migrationBuilder, "brsm-nafta", "https://brsm-nafta.com/");
            SetWebsite(migrationBuilder, "okko", "https://www.okko.ua/");
            SetWebsite(migrationBuilder, "ukrnafta", "https://www.ukrnafta.com/");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ClearWebsite(migrationBuilder, "amic", "https://amicenergy.com.ua/");
            ClearWebsite(migrationBuilder, "marshal", "https://www.azski.com.ua/");
            ClearWebsite(migrationBuilder, "ovis", "https://ovis.ua/");
            ClearWebsite(migrationBuilder, "shell", "https://www.shell.ua/");
            ClearWebsite(migrationBuilder, "ugo", "https://ugo.ua/");
            ClearWebsite(migrationBuilder, "wog", "https://wog.ua/");
            ClearWebsite(migrationBuilder, "avias", "https://avias.ua/");
            ClearWebsite(migrationBuilder, "brsm-nafta", "https://brsm-nafta.com/");
            ClearWebsite(migrationBuilder, "okko", "https://www.okko.ua/");
            ClearWebsite(migrationBuilder, "ukrnafta", "https://www.ukrnafta.com/");
        }

        private static void SetWebsite(MigrationBuilder migrationBuilder, string normalizedKey, string websiteUrl)
        {
            migrationBuilder.Sql($"""
                UPDATE `stations`
                SET `WebsiteUrl` = '{websiteUrl}'
                WHERE `NormalizedKey` = '{normalizedKey}'
                  AND (`WebsiteUrl` IS NULL OR `WebsiteUrl` = '');
                """);
        }

        private static void ClearWebsite(MigrationBuilder migrationBuilder, string normalizedKey, string websiteUrl)
        {
            migrationBuilder.Sql($"""
                UPDATE `stations`
                SET `WebsiteUrl` = NULL
                WHERE `NormalizedKey` = '{normalizedKey}'
                  AND `WebsiteUrl` = '{websiteUrl}';
                """);
        }
    }
}
