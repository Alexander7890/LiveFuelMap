using LiveFuelMap.Infrastructure.Auth;

namespace LiveFuelMap.Tests.Unit;

public sealed class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsBCryptHash()
    {
        var hash = _hasher.Hash("password123");

        Assert.StartsWith("$2", hash);
        Assert.NotEqual("password123", hash);
    }

    [Fact]
    public void Hash_ReturnsDifferentSaltedHashes_ForSamePassword()
    {
        var first = _hasher.Hash("password123");
        var second = _hasher.Hash("password123");

        Assert.NotEqual(first, second);
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
