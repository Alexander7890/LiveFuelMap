using System.Globalization;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LiveFuelMap.BLL.Services;

public sealed class FuelPriceReportService(IUnitOfWork unitOfWork) : IFuelPriceReportService
{
    private static readonly CultureInfo UkrainianCulture = CultureInfo.GetCultureInfo("uk-UA");
    private static readonly HashSet<string> SupportedFuelCodes = ["a95plus", "a95", "a92", "diesel", "gas"];

    public async Task<FuelPriceExportFile> ExportAsync(FuelPriceExportRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRequest(request);
        var data = await BuildReportDataAsync(normalized, cancellationToken);
        if (data.Rows.Count == 0)
            throw new InvalidOperationException("За обраними фільтрами не знайдено цін на пальне.");

        return normalized.Format switch
        {
            "excel" or "xlsx" => new FuelPriceExportFile(
                BuildFileName("fuel-prices-report", normalized, "xlsx"),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildExcel(data)),
            "pdf" => new FuelPriceExportFile(
                BuildFileName("fuel-prices-report", normalized, "pdf"),
                "application/pdf",
                BuildPdf(data)),
            _ => throw new InvalidOperationException("Непідтримуваний формат експорту. Оберіть PDF або Excel.")
        };
    }

    private async Task<FuelPriceReportData> BuildReportDataAsync(NormalizedFuelPriceExportRequest request, CancellationToken cancellationToken)
    {
        var query = unitOfWork.FuelPrices.Query()
            .AsNoTracking()
            .Include(x => x.Station)
            .Include(x => x.Fuel)
            .Where(x => x.Station.IsActive && x.Date.Date >= request.From && x.Date.Date <= request.To);

        if (!string.IsNullOrWhiteSpace(request.City))
            query = query.Where(x => x.Station.City == request.City);

        if (request.StationIds.Count > 0)
            query = query.Where(x => request.StationIds.Contains(x.StationId));

        if (request.FuelCodes.Count > 0)
            query = query.Where(x => request.FuelCodes.Contains(x.Fuel.Code));

        var rawRows = await query
            .OrderBy(x => x.Station.Name)
            .ThenBy(x => x.Fuel.SortOrder)
            .ThenBy(x => x.Date)
            .Select(x => new RawReportPriceRow(
                x.StationId,
                x.Station.Name,
                x.Station.City,
                x.FuelId,
                x.Fuel.Code,
                x.Fuel.Name,
                x.Date.Date,
                x.Price))
            .ToListAsync(cancellationToken);

        var rows = new List<ReportPriceRow>(rawRows.Count);
        foreach (var group in rawRows.GroupBy(x => new { x.StationId, x.FuelId }))
        {
            decimal? previous = null;
            foreach (var row in group.OrderBy(x => x.Date))
            {
                decimal? change = previous is null ? null : row.Price - previous.Value;
                decimal? changePercent = previous is null || previous == 0
                    ? null
                    : Math.Round(change!.Value / previous.Value * 100, 2);

                rows.Add(new ReportPriceRow(
                    row.StationId,
                    row.StationName,
                    row.City,
                    row.FuelId,
                    row.FuelCode,
                    row.FuelName,
                    row.Date,
                    row.Price,
                    previous,
                    change,
                    changePercent));

                previous = row.Price;
            }
        }

        rows = rows
            .OrderBy(x => x.StationName)
            .ThenBy(x => x.FuelName)
            .ThenBy(x => x.Date)
            .ToList();

        var statistics = rows
            .GroupBy(x => new { x.StationId, x.StationName, x.City, x.FuelId, x.FuelCode, x.FuelName })
            .Select(group =>
            {
                var ordered = group.OrderBy(x => x.Date).ToList();
                var first = ordered.First().Price;
                var last = ordered.Last().Price;
                return new ReportStatisticRow(
                    group.Key.StationId,
                    group.Key.StationName,
                    group.Key.City,
                    group.Key.FuelId,
                    group.Key.FuelCode,
                    group.Key.FuelName,
                    group.Min(x => x.Price),
                    group.Max(x => x.Price),
                    Math.Round(group.Average(x => x.Price), 2),
                    first,
                    last,
                    last - first,
                    first == 0 ? null : Math.Round((last - first) / first * 100, 2));
            })
            .OrderBy(x => x.FuelName)
            .ThenBy(x => x.AveragePrice)
            .ThenBy(x => x.StationName)
            .ToList();

        var comparison = statistics
            .GroupBy(x => new { x.StationId, x.StationName, x.City })
            .Select(group => new ReportComparisonRow(
                group.Key.StationId,
                group.Key.StationName,
                group.Key.City,
                Math.Round(group.Average(x => x.AveragePrice), 2),
                group.Min(x => x.MinPrice),
                group.Max(x => x.MaxPrice),
                group.Count()))
            .OrderBy(x => x.AveragePrice)
            .ThenBy(x => x.StationName)
            .ToList();

        var minShares = rows
            .GroupBy(x => new { x.Date, x.FuelId })
            .SelectMany(group =>
            {
                var min = group.Min(x => x.Price);
                return group.Where(x => x.Price == min);
            })
            .GroupBy(x => new { x.StationId, x.StationName })
            .Select(group => new ReportMinShareRow(group.Key.StationId, group.Key.StationName, group.Count()))
            .OrderByDescending(x => x.MinPriceCount)
            .ThenBy(x => x.StationName)
            .ToList();

        var filters = new ReportFilterSummary(
            string.IsNullOrWhiteSpace(request.City) ? "Усі міста" : request.City,
            request.StationIds.Count == 0 ? "Усі АЗС" : string.Join(", ", rows.Select(x => x.StationName).Distinct().OrderBy(x => x)),
            request.FuelCodes.Count == 0 ? "Усе пальне" : string.Join(", ", rows.Select(x => x.FuelName).Distinct().OrderBy(x => x)),
            request.From,
            request.To,
            request.Format);

        return new FuelPriceReportData(filters, rows, statistics, comparison, minShares, BuildConclusion(statistics, minShares));
    }

    private static byte[] BuildExcel(FuelPriceReportData data)
    {
        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "LiveFuelMap fuel price report";
        workbook.Properties.Author = "LiveFuelMap";
        workbook.Properties.Created = DateTime.UtcNow;

        AddPricesSheet(workbook, data);
        AddChangesSheet(workbook, data);
        AddStatisticsSheet(workbook, data);
        AddChartsSheet(workbook, data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddPricesSheet(XLWorkbook workbook, FuelPriceReportData data)
    {
        var ws = workbook.Worksheets.Add("Ціни");
        WriteReportHeader(ws, data, "Основна таблиця цін за обраний період", 8);
        var row = 7;
        WriteHeader(ws, row, ["Дата", "Місто", "Оператор", "Тип пального", "Ціна, грн/л", "Попередня ціна", "Зміна", "% зміни"]);

        foreach (var item in data.Rows)
        {
            row++;
            ws.Cell(row, 1).Value = item.Date;
            ws.Cell(row, 2).Value = item.City;
            ws.Cell(row, 3).Value = item.StationName;
            ws.Cell(row, 4).Value = item.FuelName;
            ws.Cell(row, 5).Value = item.Price;
            ws.Cell(row, 6).Value = item.PreviousPrice;
            ws.Cell(row, 7).Value = item.Change;
            ws.Cell(row, 8).Value = item.ChangePercent is null ? null : item.ChangePercent / 100;
        }

        FormatTable(ws, 7, row, 8);
        ws.Column(1).Style.DateFormat.Format = "dd.mm.yyyy";
        ws.Columns(5, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(8).Style.NumberFormat.Format = "0.00%";
        ws.SheetView.FreezeRows(7);
        ws.Columns().AdjustToContents();
    }

    private static void AddChangesSheet(XLWorkbook workbook, FuelPriceReportData data)
    {
        var ws = workbook.Worksheets.Add("Зміни");
        WriteReportHeader(ws, data, "Таблиці зміни цін та відсоткової зміни", 8);
        var row = 7;
        WriteHeader(ws, row, ["Дата", "Оператор", "Тип пального", "Ціна до", "Ціна після", "Зміна, грн", "% зміни", "Напрям"]);

        foreach (var item in data.Rows.Where(x => x.Change is not null))
        {
            row++;
            ws.Cell(row, 1).Value = item.Date;
            ws.Cell(row, 2).Value = item.StationName;
            ws.Cell(row, 3).Value = item.FuelName;
            ws.Cell(row, 4).Value = item.PreviousPrice;
            ws.Cell(row, 5).Value = item.Price;
            ws.Cell(row, 6).Value = item.Change;
            ws.Cell(row, 7).Value = item.ChangePercent is null ? null : item.ChangePercent / 100;
            ws.Cell(row, 8).Value = item.Change switch
            {
                > 0 => "Зростання",
                < 0 => "Зниження",
                _ => "Без змін"
            };
        }

        FormatTable(ws, 7, Math.Max(row, 8), 8);
        ws.Column(1).Style.DateFormat.Format = "dd.mm.yyyy";
        ws.Columns(4, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(7).Style.NumberFormat.Format = "0.00%";
        ws.SheetView.FreezeRows(7);
        ws.Columns().AdjustToContents();
    }

    private static void AddStatisticsSheet(XLWorkbook workbook, FuelPriceReportData data)
    {
        var ws = workbook.Worksheets.Add("Статистика");
        WriteReportHeader(ws, data, "Мінімальна, максимальна, середня ціна та порівняння АЗС", 10);
        var row = 7;
        WriteHeader(ws, row, ["Оператор", "Місто", "Тип пального", "Мін.", "Макс.", "Середня", "Перша", "Остання", "Зміна", "% зміни"]);

        foreach (var item in data.Statistics)
        {
            row++;
            ws.Cell(row, 1).Value = item.StationName;
            ws.Cell(row, 2).Value = item.City;
            ws.Cell(row, 3).Value = item.FuelName;
            ws.Cell(row, 4).Value = item.MinPrice;
            ws.Cell(row, 5).Value = item.MaxPrice;
            ws.Cell(row, 6).Value = item.AveragePrice;
            ws.Cell(row, 7).Value = item.FirstPrice;
            ws.Cell(row, 8).Value = item.LastPrice;
            ws.Cell(row, 9).Value = item.TotalChange;
            ws.Cell(row, 10).Value = item.TotalChangePercent is null ? null : item.TotalChangePercent / 100;
        }

        FormatTable(ws, 7, row, 10);
        ws.Columns(4, 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Column(10).Style.NumberFormat.Format = "0.00%";

        row += 3;
        ws.Cell(row, 1).Value = "Порівняння АЗС між собою";
        ws.Range(row, 1, row, 7).Merge().Style.Font.SetBold().Font.FontSize = 14;
        row++;
        WriteHeader(ws, row, ["Оператор", "Місто", "Середня ціна", "Мін.", "Макс.", "К-сть видів пального", "Рейтинг"]);
        var rank = 1;
        foreach (var item in data.Comparison)
        {
            row++;
            ws.Cell(row, 1).Value = item.StationName;
            ws.Cell(row, 2).Value = item.City;
            ws.Cell(row, 3).Value = item.AveragePrice;
            ws.Cell(row, 4).Value = item.MinPrice;
            ws.Cell(row, 5).Value = item.MaxPrice;
            ws.Cell(row, 6).Value = item.FuelKinds;
            ws.Cell(row, 7).Value = rank++;
        }

        FormatTable(ws, row - data.Comparison.Count, row, 7);
        ws.Columns().AdjustToContents();
    }

    private static void AddChartsSheet(XLWorkbook workbook, FuelPriceReportData data)
    {
        var ws = workbook.Worksheets.Add("Графіки");
        ws.Cell(1, 1).Value = "Дані для графіків і діаграм";
        ws.Range(1, 1, 1, 8).Merge().Style.Font.SetBold().Font.FontSize = 16;
        ws.Cell(2, 1).Value = "Excel-аркуш містить готові табличні ряди для побудови графіків: лінійний графік за датами, стовпчасте порівняння АЗС і кругову діаграму частки мінімальних цін.";
        ws.Range(2, 1, 2, 8).Merge().Style.Alignment.WrapText = true;

        var row = 4;
        ws.Cell(row, 1).Value = "Лінійний графік: середня ціна за датами";
        ws.Range(row, 1, row, 6).Merge().Style.Font.SetBold();
        row++;
        WriteHeader(ws, row, ["Дата", "Тип пального", "Середня ціна"]);
        foreach (var item in data.Rows.GroupBy(x => new { x.Date, x.FuelName }).OrderBy(x => x.Key.Date).ThenBy(x => x.Key.FuelName))
        {
            row++;
            ws.Cell(row, 1).Value = item.Key.Date;
            ws.Cell(row, 2).Value = item.Key.FuelName;
            ws.Cell(row, 3).Value = Math.Round(item.Average(x => x.Price), 2);
        }

        FormatTable(ws, 5, row, 3);
        ws.Column(1).Style.DateFormat.Format = "dd.mm.yyyy";
        ws.Column(3).Style.NumberFormat.Format = "#,##0.00";

        row += 3;
        ws.Cell(row, 1).Value = "Стовпчаста діаграма: середня ціна між АЗС";
        ws.Range(row, 1, row, 6).Merge().Style.Font.SetBold();
        row++;
        WriteHeader(ws, row, ["Оператор", "Середня ціна", "Візуально"]);
        var maxAverage = data.Comparison.Count == 0 ? 1 : data.Comparison.Max(x => x.AveragePrice);
        foreach (var item in data.Comparison)
        {
            row++;
            ws.Cell(row, 1).Value = item.StationName;
            ws.Cell(row, 2).Value = item.AveragePrice;
            ws.Cell(row, 3).Value = BuildBar(item.AveragePrice, maxAverage);
            ws.Cell(row, 3).Style.Font.FontColor = XLColor.FromHtml("#0f8b8d");
        }

        FormatTable(ws, row - data.Comparison.Count, row, 3);

        row += 3;
        ws.Cell(row, 1).Value = "Кругова діаграма: частка АЗС із мінімальними цінами";
        ws.Range(row, 1, row, 6).Merge().Style.Font.SetBold();
        row++;
        WriteHeader(ws, row, ["Оператор", "К-сть мінімумів", "Частка", "Візуально"]);
        var totalMinWins = Math.Max(1, data.MinShares.Sum(x => x.MinPriceCount));
        foreach (var item in data.MinShares)
        {
            row++;
            var share = (decimal)item.MinPriceCount / totalMinWins;
            ws.Cell(row, 1).Value = item.StationName;
            ws.Cell(row, 2).Value = item.MinPriceCount;
            ws.Cell(row, 3).Value = share;
            ws.Cell(row, 4).Value = BuildBar(share, 1);
            ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#e08f3e");
        }

        FormatTable(ws, row - data.MinShares.Count, row, 4);
        ws.Column(3).Style.NumberFormat.Format = "0.00%";
        ws.Columns().AdjustToContents();
    }

    private static byte[] BuildPdf(FuelPriceReportData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));
                page.Header().Column(column =>
                {
                    column.Item().Text("LiveFuelMap: звіт за цінами на пальне").FontSize(18).Bold().FontColor(Colors.Teal.Darken3);
                    column.Item().Text($"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(c => AddPdfFilters(c, data));
                    column.Item().Text("Автоматичний висновок").FontSize(13).Bold();
                    column.Item().Text(data.Conclusion).FontSize(9);

                    column.Item().Text("Лінійний графік зміни ціни за датами").FontSize(12).Bold();
                    column.Item().Height(210).Svg(BuildLineChartSvg(data));
                    column.Item().Text("Стовпчаста діаграма порівняння АЗС").FontSize(12).Bold();
                    column.Item().Height(190).Svg(BuildBarChartSvg(data));
                    column.Item().Text("Кругова діаграма частки АЗС з мінімальними цінами").FontSize(12).Bold();
                    column.Item().Height(190).Svg(BuildPieChartSvg(data));

                    column.Item().PageBreak();
                    column.Item().Text("Основна таблиця цін").FontSize(13).Bold();
                    column.Item().Element(c => AddPdfPriceTable(c, data.Rows.Take(120).ToList()));
                    if (data.Rows.Count > 120)
                        column.Item().Text($"У PDF показано перші 120 рядків із {data.Rows.Count}. Повний набір даних доступний в Excel.").Italic().FontColor(Colors.Grey.Darken1);

                    column.Item().Text("Статистика").FontSize(13).Bold();
                    column.Item().Element(c => AddPdfStatisticsTable(c, data.Statistics.Take(80).ToList()));
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("LiveFuelMap · ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void AddPdfFilters(IContainer container, FuelPriceReportData data)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Item().Text("Обрані фільтри").FontSize(12).Bold();
            column.Item().Text($"Місто: {data.Filters.City}");
            column.Item().Text($"АЗС: {data.Filters.Stations}");
            column.Item().Text($"Пальне: {data.Filters.Fuels}");
            column.Item().Text($"Період: {data.Filters.From:dd.MM.yyyy} - {data.Filters.To:dd.MM.yyyy}");
        });
    }

    private static void AddPdfPriceTable(IContainer container, IReadOnlyList<ReportPriceRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });
            AddPdfHeader(table, ["Дата", "Місто", "Оператор", "Пальне", "Ціна", "Зміна", "%"]);
            foreach (var row in rows)
            {
                AddPdfCell(table, row.Date.ToString("dd.MM.yyyy"));
                AddPdfCell(table, row.City);
                AddPdfCell(table, row.StationName);
                AddPdfCell(table, row.FuelName);
                AddPdfCell(table, FormatMoney(row.Price));
                AddPdfCell(table, FormatNullableMoney(row.Change));
                AddPdfCell(table, FormatPercent(row.ChangePercent));
            }
        });
    }

    private static void AddPdfStatisticsTable(IContainer container, IReadOnlyList<ReportStatisticRow> rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.6f);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
                columns.RelativeColumn(1);
            });
            AddPdfHeader(table, ["Оператор", "Пальне", "Мін.", "Макс.", "Середня", "Зміна", "%"]);
            foreach (var row in rows)
            {
                AddPdfCell(table, row.StationName);
                AddPdfCell(table, row.FuelName);
                AddPdfCell(table, FormatMoney(row.MinPrice));
                AddPdfCell(table, FormatMoney(row.MaxPrice));
                AddPdfCell(table, FormatMoney(row.AveragePrice));
                AddPdfCell(table, FormatMoney(row.TotalChange));
                AddPdfCell(table, FormatPercent(row.TotalChangePercent));
            }
        });
    }

    private static void AddPdfHeader(TableDescriptor table, IReadOnlyList<string> headers)
    {
        table.Header(header =>
        {
            foreach (var value in headers)
            {
                header.Cell().Background(Colors.Teal.Darken3).Padding(3).Text(value).FontColor(Colors.White).Bold();
            }
        });
    }

    private static void AddPdfCell(TableDescriptor table, string value)
    {
        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value);
    }

    private static string BuildLineChartSvg(FuelPriceReportData data)
    {
        var series = data.Rows
            .GroupBy(x => x.FuelName)
            .Select(group => new ChartSeries(
                group.Key,
                group.GroupBy(x => x.Date)
                    .OrderBy(x => x.Key)
                    .Select(x => new ChartPoint(x.Key, Math.Round(x.Average(y => y.Price), 2)))
                    .ToList()))
            .Where(x => x.Points.Count > 0)
            .Take(5)
            .ToList();

        return SvgLineChart(series, 540, 210);
    }

    private static string BuildBarChartSvg(FuelPriceReportData data)
    {
        var items = data.Comparison.Take(10).Select(x => new LabelValue(x.StationName, x.AveragePrice)).ToList();
        return SvgBarChart(items, 540, 190, "#0f8b8d");
    }

    private static string BuildPieChartSvg(FuelPriceReportData data)
    {
        var total = Math.Max(1, data.MinShares.Sum(x => x.MinPriceCount));
        var items = data.MinShares.Take(8).Select(x => new LabelValue(x.StationName, (decimal)x.MinPriceCount / total)).ToList();
        return SvgPieChart(items, 540, 190);
    }

    private static string SvgLineChart(IReadOnlyList<ChartSeries> series, int width, int height)
    {
        var allValues = series.SelectMany(x => x.Points.Select(p => p.Value)).ToList();
        if (allValues.Count == 0)
            return EmptySvg(width, height, "Недостатньо даних для графіка");

        var min = allValues.Min();
        var max = allValues.Max();
        if (min == max) max = min + 1;
        const int left = 48;
        const int top = 18;
        var plotWidth = width - 80;
        var plotHeight = height - 52;
        var colors = new[] { "#0f8b8d", "#e08f3e", "#315c72", "#8d5a97", "#138a4f" };

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        svg.Append(CultureInfo.InvariantCulture, $"""<rect width="100%" height="100%" fill="#ffffff"/><line x1="{left}" y1="{top}" x2="{left}" y2="{top + plotHeight}" stroke="#d7e0e3"/><line x1="{left}" y1="{top + plotHeight}" x2="{left + plotWidth}" y2="{top + plotHeight}" stroke="#d7e0e3"/>""");
        for (var s = 0; s < series.Count; s++)
        {
            var points = series[s].Points;
            var color = colors[s % colors.Length];
            var path = new StringBuilder();
            for (var i = 0; i < points.Count; i++)
            {
                var x = left + (points.Count == 1 ? plotWidth / 2m : i * plotWidth / (decimal)(points.Count - 1));
                var y = top + (max - points[i].Value) / (max - min) * plotHeight;
                path.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? "M" : "L")}{x:0.##},{y:0.##} ");
            }

            svg.Append(CultureInfo.InvariantCulture, $"""<path d="{path}" fill="none" stroke="{color}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>""");
            svg.Append(CultureInfo.InvariantCulture, $"""<text x="{left + s * 125}" y="{height - 12}" font-size="11" fill="{color}">{Escape(series[s].Label)}</text>""");
        }

        svg.Append(CultureInfo.InvariantCulture, $"""<text x="8" y="24" font-size="10" fill="#52666d">{max:0.##}</text><text x="8" y="{top + plotHeight}" font-size="10" fill="#52666d">{min:0.##}</text></svg>""");
        return svg.ToString();
    }

    private static string SvgBarChart(IReadOnlyList<LabelValue> items, int width, int height, string color)
    {
        if (items.Count == 0)
            return EmptySvg(width, height, "Недостатньо даних для діаграми");

        var max = Math.Max(1, items.Max(x => x.Value));
        const int left = 120;
        const int top = 16;
        var barHeight = Math.Max(10, (height - 36) / items.Count - 4);
        var plotWidth = width - left - 36;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        svg.Append("""<rect width="100%" height="100%" fill="#ffffff"/>""");
        for (var i = 0; i < items.Count; i++)
        {
            var y = top + i * (barHeight + 6);
            var w = Math.Max(3, items[i].Value / max * plotWidth);
            svg.Append(CultureInfo.InvariantCulture, $"""<text x="4" y="{y + barHeight - 1}" font-size="10" fill="#20323a">{Escape(Shorten(items[i].Label, 18))}</text>""");
            svg.Append(CultureInfo.InvariantCulture, $"""<rect x="{left}" y="{y}" width="{w:0.##}" height="{barHeight}" rx="4" fill="{color}"/>""");
            svg.Append(CultureInfo.InvariantCulture, $"""<text x="{left + w + 6:0.##}" y="{y + barHeight - 1}" font-size="10" fill="#52666d">{items[i].Value:0.##}</text>""");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string SvgPieChart(IReadOnlyList<LabelValue> items, int width, int height)
    {
        if (items.Count == 0)
            return EmptySvg(width, height, "Недостатньо даних для кругової діаграми");

        var colors = new[] { "#0f8b8d", "#e08f3e", "#315c72", "#8d5a97", "#138a4f", "#d65656", "#6c7a89", "#b28a00" };
        var total = items.Sum(x => x.Value);
        if (total <= 0) total = 1;
        const decimal cx = 96;
        const decimal cy = 95;
        const decimal r = 70;
        var angle = -90m;
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        svg.Append("""<rect width="100%" height="100%" fill="#ffffff"/>""");

        for (var i = 0; i < items.Count; i++)
        {
            var sweep = items[i].Value / total * 360m;
            var path = DescribeArc(cx, cy, r, angle, angle + sweep);
            svg.Append(CultureInfo.InvariantCulture, $"""<path d="{path}" fill="{colors[i % colors.Length]}" stroke="#fff" stroke-width="1"/>""");
            var legendY = 28 + i * 18;
            svg.Append(CultureInfo.InvariantCulture, $"""<rect x="210" y="{legendY - 10}" width="12" height="12" fill="{colors[i % colors.Length]}"/>""");
            svg.Append(CultureInfo.InvariantCulture, $"""<text x="230" y="{legendY}" font-size="11" fill="#20323a">{Escape(Shorten(items[i].Label, 32))} - {(items[i].Value / total * 100):0.#}%</text>""");
            angle += sweep;
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string DescribeArc(decimal cx, decimal cy, decimal r, decimal startAngle, decimal endAngle)
    {
        var start = PolarToCartesian(cx, cy, r, endAngle);
        var end = PolarToCartesian(cx, cy, r, startAngle);
        var largeArc = endAngle - startAngle <= 180 ? 0 : 1;
        return string.Create(CultureInfo.InvariantCulture, $"M {cx} {cy} L {start.X:0.##} {start.Y:0.##} A {r} {r} 0 {largeArc} 0 {end.X:0.##} {end.Y:0.##} Z");
    }

    private static (decimal X, decimal Y) PolarToCartesian(decimal cx, decimal cy, decimal r, decimal angle)
    {
        var radians = (double)(angle - 90) * Math.PI / 180.0;
        return (cx + r * (decimal)Math.Cos(radians), cy + r * (decimal)Math.Sin(radians));
    }

    private static string EmptySvg(int width, int height, string text) =>
        $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}"><rect width="100%" height="100%" fill="#fff"/><text x="20" y="40" font-size="14" fill="#52666d">{Escape(text)}</text></svg>""";

    private static void WriteReportHeader(IXLWorksheet ws, FuelPriceReportData data, string title, int width)
    {
        ws.Cell(1, 1).Value = "LiveFuelMap";
        ws.Cell(1, 1).Style.Font.SetBold().Font.FontSize = 16;
        ws.Cell(2, 1).Value = title;
        ws.Range(2, 1, 2, width).Merge().Style.Font.SetBold();
        ws.Cell(3, 1).Value = $"Дата формування: {DateTime.Now:dd.MM.yyyy HH:mm}";
        ws.Cell(4, 1).Value = $"Фільтри: місто - {data.Filters.City}; АЗС - {data.Filters.Stations}; пальне - {data.Filters.Fuels}; період - {data.Filters.From:dd.MM.yyyy} - {data.Filters.To:dd.MM.yyyy}";
        ws.Range(4, 1, 4, width).Merge().Style.Alignment.WrapText = true;
    }

    private static void WriteHeader(IXLWorksheet ws, int row, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#20323a");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void FormatTable(IXLWorksheet ws, int firstRow, int lastRow, int lastColumn)
    {
        var range = ws.Range(firstRow, 1, Math.Max(firstRow, lastRow), lastColumn);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#d9e1e4");
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#d9e1e4");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static NormalizedFuelPriceExportRequest NormalizeRequest(FuelPriceExportRequest request)
    {
        var format = string.IsNullOrWhiteSpace(request.Format)
            ? "pdf"
            : request.Format.Trim().ToLowerInvariant();

        if (format is "xlsx") format = "excel";
        if (format is not "pdf" and not "excel")
            throw new InvalidOperationException("Формат звіту має бути PDF або Excel.");

        var from = request.From.Date;
        var to = request.To.Date;
        if (from == default || to == default)
            throw new InvalidOperationException("Потрібно вказати дату початку та дату завершення.");
        if (from > to)
            throw new InvalidOperationException("Дата початку не може бути пізнішою за дату завершення.");
        if ((to - from).TotalDays > 730)
            throw new InvalidOperationException("Період експорту не може бути довшим за 2 роки.");

        var fuelCodes = (request.FuelCodes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fuelCodes.Any(x => !SupportedFuelCodes.Contains(x)))
            throw new InvalidOperationException("Непідтримуваний тип пального. Доступні коди: a95plus, a95, a92, diesel, gas.");

        var stationIds = (request.StationIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .Take(100)
            .ToList();

        var city = string.IsNullOrWhiteSpace(request.City)
            ? null
            : request.City.Trim();

        return new NormalizedFuelPriceExportRequest(city, stationIds, fuelCodes, from, to, format);
    }

    private static string BuildConclusion(IReadOnlyList<ReportStatisticRow> statistics, IReadOnlyList<ReportMinShareRow> minShares)
    {
        if (statistics.Count == 0)
            return "За обраний період недостатньо даних для автоматичного висновку.";

        var lowest = statistics.OrderBy(x => x.AveragePrice).First();
        var highestGrowth = statistics
            .Where(x => x.TotalChangePercent is not null)
            .OrderByDescending(x => x.TotalChangePercent)
            .FirstOrDefault();
        var minLeader = minShares.FirstOrDefault();

        var conclusion = $"За обраний період найнижча середня ціна на {lowest.FuelName} була на АЗС {lowest.StationName}: {FormatMoney(lowest.AveragePrice)} грн/л.";
        if (highestGrowth is not null)
            conclusion += $" Максимальне зростання ціни склало {FormatPercent(highestGrowth.TotalChangePercent)} на {highestGrowth.FuelName} у мережі {highestGrowth.StationName}.";
        if (minLeader is not null)
            conclusion += $" Найчастіше мінімальні ціни зустрічалися у {minLeader.StationName}.";

        return conclusion;
    }

    private static string BuildFileName(string prefix, NormalizedFuelPriceExportRequest request, string extension) =>
        $"{prefix}-{request.From:yyyyMMdd}-{request.To:yyyyMMdd}.{extension}";

    private static string BuildBar(decimal value, decimal max)
    {
        var count = (int)Math.Clamp(Math.Round(value / Math.Max(0.01m, max) * 28), 1, 28);
        return new string('█', count);
    }

    private static string FormatMoney(decimal value) => value.ToString("0.00", UkrainianCulture);
    private static string FormatNullableMoney(decimal? value) => value is null ? "-" : FormatMoney(value.Value);
    private static string FormatPercent(decimal? value) => value is null ? "-" : value.Value.ToString("+0.##;-0.##;0", UkrainianCulture) + "%";
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
    private static string Shorten(string value, int length) => value.Length <= length ? value : value[..Math.Max(0, length - 1)] + "…";

    private sealed record NormalizedFuelPriceExportRequest(string? City, IReadOnlyList<int> StationIds, IReadOnlyList<string> FuelCodes, DateTime From, DateTime To, string Format);
    private sealed record RawReportPriceRow(int StationId, string StationName, string City, int FuelId, string FuelCode, string FuelName, DateTime Date, decimal Price);
    private sealed record FuelPriceReportData(ReportFilterSummary Filters, IReadOnlyList<ReportPriceRow> Rows, IReadOnlyList<ReportStatisticRow> Statistics, IReadOnlyList<ReportComparisonRow> Comparison, IReadOnlyList<ReportMinShareRow> MinShares, string Conclusion);
    private sealed record ReportFilterSummary(string City, string Stations, string Fuels, DateTime From, DateTime To, string Format);
    private sealed record ReportPriceRow(int StationId, string StationName, string City, int FuelId, string FuelCode, string FuelName, DateTime Date, decimal Price, decimal? PreviousPrice, decimal? Change, decimal? ChangePercent);
    private sealed record ReportStatisticRow(int StationId, string StationName, string City, int FuelId, string FuelCode, string FuelName, decimal MinPrice, decimal MaxPrice, decimal AveragePrice, decimal FirstPrice, decimal LastPrice, decimal TotalChange, decimal? TotalChangePercent);
    private sealed record ReportComparisonRow(int StationId, string StationName, string City, decimal AveragePrice, decimal MinPrice, decimal MaxPrice, int FuelKinds);
    private sealed record ReportMinShareRow(int StationId, string StationName, int MinPriceCount);
    private sealed record ChartPoint(DateTime Date, decimal Value);
    private sealed record ChartSeries(string Label, IReadOnlyList<ChartPoint> Points);
    private sealed record LabelValue(string Label, decimal Value);
}
