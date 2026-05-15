using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedTextEncoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "? 95+");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "? 95");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "? 92");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "??");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "???");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "City" },
                values: new object[] { "???????? ?????? ???????, 142A", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ???????? ?????????, 85", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ???????????, 98?", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Address", "City" },
                values: new object[] { "????????????? ????????, 223", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ?????????", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Address", "City" },
                values: new object[] { "???????? ????? ??????????", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Address", "City" },
                values: new object[] { "????????????? ????????, 127?", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ????????, 41", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "???????? ???????", "??????", "?????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ??????????????, 2?", "??????", "????-?????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ???????????, 44", "??????", "????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ????????? ???????", "??????", "????????" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 1,
                column: "Name",
                value: "? 95+");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 2,
                column: "Name",
                value: "? 95");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "? 92");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 4,
                column: "Name",
                value: "??");

            migrationBuilder.UpdateData(
                table: "fuels",
                keyColumn: "Id",
                keyValue: 5,
                column: "Name",
                value: "???");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Address", "City" },
                values: new object[] { "???????? ?????? ???????, 142A", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ???????? ?????????, 85", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ???????????, 98?", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Address", "City" },
                values: new object[] { "????????????? ????????, 223", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ?????????", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Address", "City" },
                values: new object[] { "???????? ????? ??????????", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Address", "City" },
                values: new object[] { "????????????? ????????, 127?", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Address", "City" },
                values: new object[] { "?????? ????????, 41", "??????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "???????? ???????", "??????", "?????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ??????????????, 2?", "??????", "????-?????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ???????????, 44", "??????", "????" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Address", "City", "Name" },
                values: new object[] { "?????? ????????? ???????", "??????", "????????" });
        }
    }
}
