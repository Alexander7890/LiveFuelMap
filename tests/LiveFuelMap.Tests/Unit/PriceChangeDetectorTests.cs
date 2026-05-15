using LiveFuelMap.BLL.Services;

namespace LiveFuelMap.Tests.Unit;

public sealed class PriceChangeDetectorTests
{
    private readonly PriceChangeDetector _detector = new();

    [Fact]
    public void IsMeaningfulChange_ReturnsTrue_WhenNoPreviousPrice()
    {
        Assert.True(_detector.IsMeaningfulChange(null, 55.10m));
    }

    [Fact]
    public void IsMeaningfulChange_ReturnsFalse_WhenPriceIsEqual()
    {
        Assert.False(_detector.IsMeaningfulChange(55.10m, 55.10m));
    }

    [Fact]
    public void IsMeaningfulChange_ReturnsFalse_WhenDifferenceIsBelowThreshold()
    {
        Assert.False(_detector.IsMeaningfulChange(55.100m, 55.105m));
    }

    [Fact]
    public void IsMeaningfulChange_ReturnsTrue_WhenPriceIncreasesByOneKopiyka()
    {
        Assert.True(_detector.IsMeaningfulChange(55.10m, 55.11m));
    }

    [Fact]
    public void IsMeaningfulChange_ReturnsTrue_WhenPriceDecreases()
    {
        Assert.True(_detector.IsMeaningfulChange(55.10m, 54.90m));
    }
}
