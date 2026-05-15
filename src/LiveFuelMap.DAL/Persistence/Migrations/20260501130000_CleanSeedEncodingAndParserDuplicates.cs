using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CleanSeedEncodingAndParserDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM fuel_prices
                WHERE StationId IN (14, 18, 22, 25);

                DELETE FROM comments
                WHERE StationId IN (14, 18, 22, 25);

                DELETE FROM stations
                WHERE Id IN (14, 18, 22, 25);

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 1 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 13;
                UPDATE comments SET StationId = 1 WHERE StationId = 13;
                UPDATE fuel_prices SET StationId = 1 WHERE StationId = 13;
                DELETE FROM stations WHERE Id = 13;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 2 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 15;
                UPDATE comments SET StationId = 2 WHERE StationId = 15;
                UPDATE fuel_prices SET StationId = 2 WHERE StationId = 15;
                DELETE FROM stations WHERE Id = 15;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 3 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 16;
                UPDATE comments SET StationId = 3 WHERE StationId = 16;
                UPDATE fuel_prices SET StationId = 3 WHERE StationId = 16;
                DELETE FROM stations WHERE Id = 16;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 4 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 17;
                UPDATE comments SET StationId = 4 WHERE StationId = 17;
                UPDATE fuel_prices SET StationId = 4 WHERE StationId = 17;
                DELETE FROM stations WHERE Id = 17;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 5 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 19;
                UPDATE comments SET StationId = 5 WHERE StationId = 19;
                UPDATE fuel_prices SET StationId = 5 WHERE StationId = 19;
                DELETE FROM stations WHERE Id = 19;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 7 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 20;
                UPDATE comments SET StationId = 7 WHERE StationId = 20;
                UPDATE fuel_prices SET StationId = 7 WHERE StationId = 20;
                DELETE FROM stations WHERE Id = 20;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 8 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 23;
                UPDATE comments SET StationId = 8 WHERE StationId = 23;
                UPDATE fuel_prices SET StationId = 8 WHERE StationId = 23;
                DELETE FROM stations WHERE Id = 23;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 10 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 24;
                UPDATE comments SET StationId = 10 WHERE StationId = 24;
                UPDATE fuel_prices SET StationId = 10 WHERE StationId = 24;
                DELETE FROM stations WHERE Id = 24;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 11 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 26;
                UPDATE comments SET StationId = 11 WHERE StationId = 26;
                UPDATE fuel_prices SET StationId = 11 WHERE StationId = 26;
                DELETE FROM stations WHERE Id = 26;

                DELETE fp FROM fuel_prices fp
                INNER JOIN fuel_prices keep_fp ON keep_fp.StationId = 12 AND keep_fp.FuelId = fp.FuelId AND keep_fp.Date = fp.Date
                WHERE fp.StationId = 21;
                UPDATE comments SET StationId = 12 WHERE StationId = 21;
                UPDATE fuel_prices SET StationId = 12 WHERE StationId = 21;
                DELETE FROM stations WHERE Id = 21;

                UPDATE fuels SET Name = 'А 95+' WHERE Id = 1;
                UPDATE fuels SET Name = 'А 95' WHERE Id = 2;
                UPDATE fuels SET Name = 'А 92' WHERE Id = 3;
                UPDATE fuels SET Name = 'ДП' WHERE Id = 4;
                UPDATE fuels SET Name = 'Газ' WHERE Id = 5;

                UPDATE stations SET Name = 'AMIC', NormalizedKey = 'amic', Address = 'проспект Героїв Харкова, 142A', City = 'Харків', Latitude = 49.966850, Longitude = 36.317010, ImageUrl = 'https://th.bing.com/th/id/OIP.3P7fCEmHgzRbvLL5VGI9WwHaFP?rs=1&pid=ImgDetMain', IsActive = TRUE WHERE Id = 1;
                UPDATE stations SET Name = 'Marshal', NormalizedKey = 'marshal', Address = 'вулиця Григорія Сковороди, 85', City = 'Харків', Latitude = 50.012210, Longitude = 36.255580, ImageUrl = 'https://www.azski.com.ua/images/networks/marshal.jpg', IsActive = TRUE WHERE Id = 2;
                UPDATE stations SET Name = 'Ovis', NormalizedKey = 'ovis', Address = 'вулиця Клочківська, 98А', City = 'Харків', Latitude = 50.005090, Longitude = 36.218900, ImageUrl = 'https://codeit4.life/app/uploads/2023/03/ovis.png', IsActive = TRUE WHERE Id = 3;
                UPDATE stations SET Name = 'Rodnik', NormalizedKey = 'rodnik', Address = 'Аерокосмічний проспект, 223', City = 'Харків', Latitude = 49.884170, Longitude = 36.291900, ImageUrl = 'https://th.bing.com/th/id/OIP.HVtgS-2xJha8xLwQ--v4PwAAAA?rs=1&pid=ImgDetMain', IsActive = TRUE WHERE Id = 4;
                UPDATE stations SET Name = 'SUN OIL', NormalizedKey = 'sun-oil', Address = 'вулиця Некрасова', City = 'Харків', Latitude = 49.947570, Longitude = 36.186600, ImageUrl = 'https://mir-s3-cdn-cf.behance.net/project_modules/fs/ea1e0a17798911.563603a213dcf.jpg', IsActive = TRUE WHERE Id = 5;
                UPDATE stations SET Name = 'Shell', NormalizedKey = 'shell', Address = 'Av. Zhukov, проспект Петра Григоренка', City = 'Харків', Latitude = 49.945000, Longitude = 36.313130, ImageUrl = 'https://vsememy.ru/kartinki/wp-content/uploads/2023/03/1643621408_7-papik-pro-p-shell-logotip-7.jpg', IsActive = TRUE WHERE Id = 6;
                UPDATE stations SET Name = 'U.GO', NormalizedKey = 'ugo', Address = '127а, Аерокосмічний проспект', City = 'Харків', Latitude = 49.964610, Longitude = 36.259940, ImageUrl = 'https://th.bing.com/th/id/OIP.1FVDZYVIATB71QU6VNKCZAHaHa?rs=1&pid=ImgDetMain', IsActive = TRUE WHERE Id = 7;
                UPDATE stations SET Name = 'WOG', NormalizedKey = 'wog', Address = 'вулиця Шевченка, 41', City = 'Харків', Latitude = 49.996090, Longitude = 36.250020, ImageUrl = 'https://th.bing.com/th/id/OIP.NY1gJdDiJy6WD6lAhRIr6AAAAA?rs=1&pid=ImgDetMain', IsActive = TRUE WHERE Id = 8;
                UPDATE stations SET Name = 'Авіас', NormalizedKey = 'avias', Address = 'просп. Байрона', City = 'Харків', Latitude = 49.945400, Longitude = 36.332170, ImageUrl = 'https://seeklogo.com/images/A/avias-logo-A0C43F9345-seeklogo.com.png', IsActive = TRUE WHERE Id = 9;
                UPDATE stations SET Name = 'БРСМ-Нафта', NormalizedKey = 'brsm-nafta', Address = 'вулиця Валентинівська, 2а', City = 'Харків', Latitude = 50.021000, Longitude = 36.319070, ImageUrl = 'https://agrorozvytok.com.ua/images/gotovo-logo-brsm-555.jpg', IsActive = TRUE WHERE Id = 10;
                UPDATE stations SET Name = 'ОККО', NormalizedKey = 'okko', Address = 'вулиця Клочківська, 44', City = 'Харків', Latitude = 49.997310, Longitude = 36.227570, ImageUrl = 'https://th.bing.com/th/id/OIP.XWGNsIqGjw97H_jWIOCgRgHaGy?w=177&h=180&c=7&r=0&o=5&pid=1.7', IsActive = TRUE WHERE Id = 11;
                UPDATE stations SET Name = 'Укрнафта', NormalizedKey = 'ukrnafta', Address = 'вулиця Академіка Павлова,', City = 'Харків', Latitude = 49.989690, Longitude = 36.287720, ImageUrl = 'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS14Iy__vrlMNJgM7WMJJaWXCALSMzIz5ADHg&s', IsActive = TRUE WHERE Id = 12;
                """);

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "https://th.bing.com/th/id/OIP.3P7fCEmHgzRbvLL5VGI9WwHaFP?rs=1&pid=ImgDetMain");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 2,
                column: "ImageUrl",
                value: "https://www.azski.com.ua/images/networks/marshal.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 3,
                column: "ImageUrl",
                value: "https://codeit4.life/app/uploads/2023/03/ovis.png");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 4,
                column: "ImageUrl",
                value: "https://th.bing.com/th/id/OIP.HVtgS-2xJha8xLwQ--v4PwAAAA?rs=1&pid=ImgDetMain");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 5,
                column: "ImageUrl",
                value: "https://mir-s3-cdn-cf.behance.net/project_modules/fs/ea1e0a17798911.563603a213dcf.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "Av. Zhukov, проспект Петра Григоренка", "https://vsememy.ru/kartinki/wp-content/uploads/2023/03/1643621408_7-papik-pro-p-shell-logotip-7.jpg" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "127а, Аерокосмічний проспект", "https://th.bing.com/th/id/OIP.1FVDZYVIATB71QU6VNKCZAHaHa?rs=1&pid=ImgDetMain" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 8,
                column: "ImageUrl",
                value: "https://th.bing.com/th/id/OIP.NY1gJdDiJy6WD6lAhRIr6AAAAA?rs=1&pid=ImgDetMain");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "просп. Байрона", "https://seeklogo.com/images/A/avias-logo-A0C43F9345-seeklogo.com.png" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 10,
                column: "ImageUrl",
                value: "https://agrorozvytok.com.ua/images/gotovo-logo-brsm-555.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 11,
                column: "ImageUrl",
                value: "https://th.bing.com/th/id/OIP.XWGNsIqGjw97H_jWIOCgRgHaGy?w=177&h=180&c=7&r=0&o=5&pid=1.7");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "вулиця Академіка Павлова,", "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS14Iy__vrlMNJgM7WMJJaWXCALSMzIz5ADHg&s" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 2,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 3,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 4,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 5,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "проспект Петра Григоренка", "default-station.jpg" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "Аерокосмічний проспект, 127а", "default-station.jpg" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 8,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "проспект Байрона", "default-station.jpg" });

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 10,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 11,
                column: "ImageUrl",
                value: "default-station.jpg");

            migrationBuilder.UpdateData(
                table: "stations",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "Address", "ImageUrl" },
                values: new object[] { "вулиця Академіка Павлова", "default-station.jpg" });
        }
    }
}
