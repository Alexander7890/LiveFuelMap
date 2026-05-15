using System.Security.Cryptography;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.Infrastructure.Auth;
using Microsoft.IdentityModel.Tokens;

namespace LiveFuelMap.Tests.Unit;

public sealed class JwtSigningKeyFactoryTests
{
    [Fact]
    public void Create_ReturnsHmacMaterial_ForHs256()
    {
        var material = JwtSigningKeyFactory.Create(new JwtOptions
        {
            Algorithm = "HS256",
            Secret = "livefuelmap-test-secret-key-32-bytes-minimum"
        });

        Assert.Equal(SecurityAlgorithms.HmacSha256, material.Algorithm);
        Assert.IsType<SymmetricSecurityKey>(material.ValidationKey);
    }

    [Fact]
    public void Create_ReturnsEcdsaMaterial_ForEs256()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var material = JwtSigningKeyFactory.Create(new JwtOptions
        {
            Algorithm = "ES256",
            PrivateKeyPem = ecdsa.ExportECPrivateKeyPem(),
            PublicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem()
        });

        Assert.Equal(SecurityAlgorithms.EcdsaSha256, material.Algorithm);
        Assert.IsType<ECDsaSecurityKey>(material.ValidationKey);
    }

    [Fact]
    public void Create_RejectsEdDsa_WhenProviderIsNotAvailable()
    {
        Assert.Throws<InvalidOperationException>(() => JwtSigningKeyFactory.Create(new JwtOptions
        {
            Algorithm = "EdDSA",
            Secret = "livefuelmap-test-secret-key-32-bytes-minimum"
        }));
    }
}
