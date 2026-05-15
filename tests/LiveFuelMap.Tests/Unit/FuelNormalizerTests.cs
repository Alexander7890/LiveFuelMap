using LiveFuelMap.BLL.Services;

namespace LiveFuelMap.Tests.Unit;

public sealed class FuelNormalizerTests
{
    private readonly UnicodeFuelNormalizer _normalizer = new();

    [Fact]
    public void NormalizeFuelCode_MapsPremiumA95()
    {
        Assert.Equal("a95plus", _normalizer.NormalizeFuelCode("А 95+"));
    }

    [Fact]
    public void NormalizeFuelCode_MapsRegularA95()
    {
        Assert.Equal("a95", _normalizer.NormalizeFuelCode("А-95"));
    }

    [Fact]
    public void NormalizeFuelCode_MapsDiesel()
    {
        Assert.Equal("diesel", _normalizer.NormalizeFuelCode("Дизельне паливо"));
    }

    [Fact]
    public void NormalizeFuelCode_MapsGas()
    {
        Assert.Equal("gas", _normalizer.NormalizeFuelCode("Газ автомобільний"));
    }

    [Fact]
    public void NormalizeStationKey_RemovesNoise()
    {
        Assert.Equal("brsm-nafta", _normalizer.NormalizeStationKey("  БРСМ-Нафта!!! "));
    }
}
