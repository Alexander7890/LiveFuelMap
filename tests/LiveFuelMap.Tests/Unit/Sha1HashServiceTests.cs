using LiveFuelMap.Infrastructure.Auth;

namespace LiveFuelMap.Tests.Unit;

public sealed class Sha1HashServiceTests
{
    private readonly Sha1HashService _hasher = new();

    [Fact]
    public void Hash_ReturnsExpectedSha1Hex()
    {
        Assert.Equal("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d", _hasher.Hash("hello"));
    }

    [Fact]
    public void Hash_ReturnsFortyHexCharacters()
    {
        var hash = _hasher.Hash("secure-password");

        Assert.Equal(40, hash.Length);
        Assert.Matches("^[0-9a-f]{40}$", hash);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForMatchingPassword()
    {
        var hash = _hasher.Hash("password123");

        Assert.True(_hasher.Verify("password123", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = _hasher.Hash("password123");

        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyHash()
    {
        Assert.False(_hasher.Verify("password123", string.Empty));
    }
}
