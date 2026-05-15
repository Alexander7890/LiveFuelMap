using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveFuelMap.DAL.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(LiveFuelMapDbContext))]
[Migration("20260501120000_SeedLegacyPricesAndImages")]
public partial class SeedLegacyPricesAndImages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE stations SET Address = 'проспект Героїв Харкова, 142A', ImageUrl = 'https://th.bing.com/th/id/OIP.3P7fCEmHgzRbvLL5VGI9WwHaFP?rs=1&pid=ImgDetMain' WHERE Id = 1;
            UPDATE stations SET Address = 'вулиця Григорія Сковороди, 85', ImageUrl = 'https://www.azski.com.ua/images/networks/marshal.jpg' WHERE Id = 2;
            UPDATE stations SET Address = 'вулиця Клочківська, 98А', ImageUrl = 'https://codeit4.life/app/uploads/2023/03/ovis.png' WHERE Id = 3;
            UPDATE stations SET Address = 'Аерокосмічний проспект, 223', ImageUrl = 'https://th.bing.com/th/id/OIP.HVtgS-2xJha8xLwQ--v4PwAAAA?rs=1&pid=ImgDetMain' WHERE Id = 4;
            UPDATE stations SET Address = 'вулиця Некрасова', ImageUrl = 'https://mir-s3-cdn-cf.behance.net/project_modules/fs/ea1e0a17798911.563603a213dcf.jpg' WHERE Id = 5;
            UPDATE stations SET Address = 'Av. Zhukov, проспект Петра Григоренка', ImageUrl = 'https://vsememy.ru/kartinki/wp-content/uploads/2023/03/1643621408_7-papik-pro-p-shell-logotip-7.jpg' WHERE Id = 6;
            UPDATE stations SET Address = '127а, Аерокосмічний проспект', ImageUrl = 'https://th.bing.com/th/id/OIP.1FVDZYVIATB71QU6VNKCZAHaHa?rs=1&pid=ImgDetMain' WHERE Id = 7;
            UPDATE stations SET Address = 'вулиця Шевченка, 41', ImageUrl = 'https://th.bing.com/th/id/OIP.NY1gJdDiJy6WD6lAhRIr6AAAAA?rs=1&pid=ImgDetMain' WHERE Id = 8;
            UPDATE stations SET Address = 'просп. Байрона', ImageUrl = 'https://seeklogo.com/images/A/avias-logo-A0C43F9345-seeklogo.com.png' WHERE Id = 9;
            UPDATE stations SET Address = 'вулиця Валентинівська, 2а', ImageUrl = 'https://agrorozvytok.com.ua/images/gotovo-logo-brsm-555.jpg' WHERE Id = 10;
            UPDATE stations SET Address = 'вулиця Клочківська, 44', ImageUrl = 'https://th.bing.com/th/id/OIP.XWGNsIqGjw97H_jWIOCgRgHaGy?w=177&h=180&c=7&r=0&o=5&pid=1.7' WHERE Id = 11;
            UPDATE stations SET Address = 'вулиця Академіка Павлова,', ImageUrl = 'https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcS14Iy__vrlMNJgM7WMJJaWXCALSMzIz5ADHg&s' WHERE Id = 12;

            INSERT INTO fuel_prices (Id, StationId, FuelId, Price, Popularity, Date, SourceId, IsManual, CreatedAt) VALUES
                (246, 1, 2, 67.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (247, 1, 4, 70.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (248, 2, 1, 70.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (249, 2, 2, 67.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (250, 2, 4, 70.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (251, 2, 5, 39.49, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (256, 3, 1, 71.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (257, 3, 2, 68.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (258, 3, 3, 65.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (259, 3, 4, 73.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (260, 3, 5, 40.49, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (261, 4, 2, 66.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (262, 4, 4, 68.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (263, 4, 5, 41.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (268, 5, 2, 66.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (269, 5, 3, 65.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (270, 5, 4, 67.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (271, 5, 5, 38.49, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (272, 7, 2, 65.90, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (273, 7, 3, 64.90, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (274, 7, 4, 65.90, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (275, 7, 5, 38.90, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (280, 8, 1, 73.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (281, 8, 2, 70.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (282, 8, 4, 75.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (283, 8, 5, 41.98, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (284, 10, 2, 66.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (285, 10, 4, 69.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (286, 10, 5, 39.49, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (290, 11, 1, 73.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (291, 11, 2, 70.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (292, 11, 4, 75.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (293, 11, 5, 41.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (294, 12, 1, 71.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (295, 12, 2, 68.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (296, 12, 3, 65.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (297, 12, 4, 69.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00'),
                (298, 12, 5, 40.99, 0, '2026-03-10', 1, FALSE, '2026-03-10 00:00:00')
            ON DUPLICATE KEY UPDATE
                Price = VALUES(Price),
                Popularity = VALUES(Popularity),
                SourceId = VALUES(SourceId),
                IsManual = VALUES(IsManual);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM fuel_prices
            WHERE Id IN (
                246, 247, 248, 249, 250, 251, 256, 257, 258, 259, 260, 261, 262, 263,
                268, 269, 270, 271, 272, 273, 274, 275, 280, 281, 282, 283, 284, 285,
                286, 290, 291, 292, 293, 294, 295, 296, 297, 298
            );

            UPDATE stations SET ImageUrl = 'default-station.jpg' WHERE Id BETWEEN 1 AND 12;
            """);
    }
}
