using System.Net;
using LiveFuelMap.DAL.Entities;
using LiveFuelMap.Infrastructure.Parsing;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiveFuelMap.Tests.Unit;

public sealed class MinfinHtmlPriceSourceClientTests
{
    [Fact]
    public async Task FetchAsync_RespectsOperatorHeaderColspanAndIconColumn()
    {
        const string html = """
            <div class="sort1-table" id="tm-table">
            <table border="0" cellspacing="0" cellpadding="3" class="zebra" style="min-width:42%">
            <caption>Харьковская обл.&nbsp;— цены операторов на&nbsp;1.05.2026 <span class="normal nowrap">(грн./литр)</span></caption>
            <tbody>
            <tr><th colspan="2" align="left" class="blank"><span class="sortspan ">Оператор</span></th><th align="right" class="blank">А&nbsp;95+</th><th align="right" class="blank">А&nbsp;95</th><th align="right" class="blank">А&nbsp;92</th><th align="right" class="blank">ДТ</th><th align="right" class="blank">Газ</th></tr>
            <tr><td align="left" class="r1"><a href="/markets/fuel/tm/amic/">AMIC</a></td><td align="center" class="r1" style="padding:3px"><br></td><td align="right" class="r1"><br></td><td align="right" class="r1">70,99</td><td align="right" class="r1"><br></td><td align="right" class="r1">87,99</td><td align="right" class="r1"><br></td></tr>
            <tr><td align="left" class="r1"><a href="/markets/fuel/tm/marshal/">Marshal</a></td><td align="center" class="r1" style="padding:3px"><br></td><td align="right" class="r1">73,99</td><td align="right" class="r1">70,99</td><td align="right" class="r1"><br></td><td align="right" class="r1">86,99</td><td align="right" class="r1">47,49</td></tr>
            </tbody></table>
            </div>
            """;

        var records = await CreateClient(html).FetchAsync(new DataSource { Url = "https://example.test/minfin" });

        Assert.DoesNotContain(records, x => x.StationName == "AMIC" && x.FuelName == "А 95+");
        Assert.Contains(records, x => x.StationName == "AMIC" && x.FuelName == "А 95" && x.Price == 70.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "AMIC" && x.FuelName == "ДП" && x.Price == 87.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "Marshal" && x.FuelName == "А 95+" && x.Price == 73.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "Marshal" && x.FuelName == "А 95" && x.Price == 70.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "Marshal" && x.FuelName == "ДП" && x.Price == 86.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "Marshal" && x.FuelName == "Газ" && x.Price == 47.49m && x.Date == new DateTime(2026, 5, 1));
    }

    [Fact]
    public async Task FetchAsync_ParsesCurrentMinfinOperatorTableShape()
    {
        const string html = """
            <html><body>
            <table>
              <caption>Харківська обл. — ціни операторів на 1.05.2026 (грн./літр)</caption>
              <thead>
                <tr><th>Оператор</th><th>А 95+</th><th>А 95</th><th>А 92</th><th>ДП</th><th>Газ</th></tr>
              </thead>
              <tbody>
                <tr><td>AMIC</td><td></td><td>70,99</td><td></td><td>87,99</td><td></td></tr>
                <tr><td>Ovis</td><td>76,49</td><td>73,49</td><td>70,49</td><td>88,90</td><td>47,49</td></tr>
              </tbody>
            </table>
            </body></html>
            """;

        var records = await CreateClient(html).FetchAsync(new DataSource { Url = "https://example.test/minfin" });

        Assert.Contains(records, x => x.StationName == "AMIC" && x.FuelName == "А 95" && x.Price == 70.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "AMIC" && x.FuelName == "ДП" && x.Price == 87.99m && x.Date == new DateTime(2026, 5, 1));
        Assert.Contains(records, x => x.StationName == "Ovis" && x.FuelName == "Газ" && x.Price == 47.49m && x.Date == new DateTime(2026, 5, 1));
    }

    [Fact]
    public async Task FetchAsync_ParsesMissingOperatorsFromMayElevenTable()
    {
        const string html = """
            <div class="sort1-table" id="tm-table">
            <table border="0" cellspacing="0" cellpadding="3" class="zebra">
            <caption>Харьковская обл.&nbsp;— цены операторов на&nbsp;11.05.2026 <span class="normal nowrap">(грн./литр)</span></caption>
            <tbody>
            <tr><th colspan="2" align="left" class="blank"><span class="sortspan ">Оператор</span></th><th align="right" class="blank">А&nbsp;95+</th><th align="right" class="blank">А&nbsp;95</th><th align="right" class="blank">А&nbsp;92</th><th align="right" class="blank">ДТ</th><th align="right" class="blank">Газ</th></tr>
            <tr><td align="left" class="r0"><a href="/markets/fuel/tm/brent_oil/">Brent Oil</a></td><td align="center" class="r0"><br></td><td align="right" class="r0"><br></td><td align="right" class="r0">66,85</td><td align="right" class="r0">64,85</td><td align="right" class="r0">86,75</td><td align="right" class="r0">47,45</td></tr>
            <tr><td align="left" class="r0"><a href="/markets/fuel/tm/socar/">SOCAR</a></td><td align="center" class="r0"><br></td><td align="right" class="r0">79,90</td><td align="right" class="r0">76,90</td><td align="right" class="r0"><br></td><td align="right" class="r0">88,90</td><td align="right" class="r0">49,98</td></tr>
            <tr><td align="left" class="r1"><a href="/markets/fuel/tm/dnipronafta/">ДНІПРОНАФТА</a></td><td align="center" class="r1"><br></td><td align="right" class="r1"><br></td><td align="right" class="r1">68,99</td><td align="right" class="r1"><br></td><td align="right" class="r1">88,99</td><td align="right" class="r1">46,99</td></tr>
            </tbody></table>
            </div>
            """;

        var records = await CreateClient(html).FetchAsync(new DataSource { Url = "https://example.test/minfin" });

        Assert.Equal(11, records.Count);
        Assert.Contains(records, x => x.StationName == "Brent Oil" && x.FuelName == "А 95" && x.Price == 66.85m && x.Date == new DateTime(2026, 5, 11));
        Assert.Contains(records, x => x.StationName == "Brent Oil" && x.FuelName == "А 92" && x.Price == 64.85m);
        Assert.Contains(records, x => x.StationName == "SOCAR" && x.FuelName == "А 95+" && x.Price == 79.90m);
        Assert.Contains(records, x => x.StationName == "SOCAR" && x.FuelName == "Газ" && x.Price == 49.98m);
        Assert.Contains(records, x => x.StationName == "ДНІПРОНАФТА" && x.FuelName == "А 95" && x.Price == 68.99m);
        Assert.Contains(records, x => x.StationName == "ДНІПРОНАФТА" && x.FuelName == "ДП" && x.Price == 88.99m);
        Assert.Contains(records, x => x.StationName == "ДНІПРОНАФТА" && x.FuelName == "Газ" && x.Price == 46.99m);
    }

    [Fact]
    public async Task FetchAsync_ParsesDateWhenCaptionIsMissing()
    {
        const string html = """
            <html><body>
            <h1>Середні ціни на пальне на 1.05.2026</h1>
            <table>
              <tr><th>Оператор</th><th>А 95+</th><th>А 95</th><th>А 92</th><th>ДП</th><th>Газ</th></tr>
              <tr><td>WOG</td><td>78,90</td><td>75,90</td><td></td><td>89,90</td><td>49,90</td></tr>
            </table>
            </body></html>
            """;

        var records = await CreateClient(html).FetchAsync(new DataSource { Url = "https://example.test/minfin" });

        Assert.Contains(records, x => x.StationName == "WOG" && x.FuelName == "А 95+" && x.Price == 78.90m && x.Date == new DateTime(2026, 5, 1));
    }

    private static MinfinHtmlPriceSourceClient CreateClient(string html)
    {
        var httpClient = new HttpClient(new StaticHtmlHandler(html));
        return new MinfinHtmlPriceSourceClient(httpClient, NullLogger<MinfinHtmlPriceSourceClient>.Instance);
    }

    private sealed class StaticHtmlHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            });
    }
}
