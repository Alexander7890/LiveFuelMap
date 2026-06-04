using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Services;

namespace LiveFuelMap.Tests.Unit;

public sealed class ChatIntentRecognitionServiceTests
{
    private readonly ChatIntentRecognitionService _service = new();

    [Theory]
    [InlineData("Яка ціна А95 на ОККО?", "fuel_price", "a95")]
    [InlineData("Де найдешевший дизель?", "fuel_statistics", "diesel")]
    [InlineData("Покажи історію цін на газ", "fuel_history", "gas")]
    [InlineData("Скільки буде коштувати 40 літрів бензину 95 на WOG?", "fuel_price", "a95")]
    public void Analyze_FuelQueries_DetectsFuelIntentAndFuelCode(string message, string expectedIntent, string expectedFuelCode)
    {
        var result = _service.Analyze(new ChatRequest(message), message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedFuelCode, result.FuelCode);
        Assert.True(result.RequiresDatabase);
    }

    [Theory]
    [InlineData("Порівняй ОККО та WOG", "station_comparison")]
    [InlineData("Які АЗС поруч?", "nearest_station")]
    [InlineData("Яка витрата у Toyota Camry 2.5?", "vehicle_question")]
    public void Analyze_NonPriceQueries_DetectsExpectedIntent(string message, string expectedIntent)
    {
        var result = _service.Analyze(new ChatRequest(message), message);

        Assert.Equal(expectedIntent, result.Intent);
    }
}
