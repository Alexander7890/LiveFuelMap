using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LiveFuelMap.BLL.DTOs;
using LiveFuelMap.BLL.Interfaces;
using LiveFuelMap.DAL.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LiveFuelMap.Infrastructure.Auth;

public sealed class Sha1HashService : ITokenHasher
{
    public string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool Verify(string value, string hash)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(hash))
            return false;

        var normalizedHash = hash.Trim().ToLowerInvariant();
        var computedHash = Hash(value);
        var computedBytes = Encoding.ASCII.GetBytes(computedHash);
        var storedBytes = Encoding.ASCII.GetBytes(normalizedHash);

        return computedBytes.Length == storedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return BCrypt.Net.BCrypt.HashPassword(value, workFactor: 11);
    }

    public bool Verify(string value, string hash) =>
        !string.IsNullOrWhiteSpace(value) &&
        !string.IsNullOrWhiteSpace(hash) &&
        BCrypt.Net.BCrypt.Verify(value, hash);
}

public sealed record JwtKeyMaterial(SigningCredentials SigningCredentials, SecurityKey ValidationKey, string Algorithm);

public static class JwtSigningKeyFactory
{
    public static JwtKeyMaterial Create(JwtOptions options)
    {
        var algorithm = NormalizeAlgorithm(options.Algorithm);
        if (algorithm.StartsWith("HS", StringComparison.Ordinal))
            return CreateHmacMaterial(options, algorithm);

        if (algorithm.StartsWith("ES", StringComparison.Ordinal))
            return CreateEcdsaMaterial(options, algorithm);

        if (string.Equals(algorithm, "EdDSA", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "EdDSA is not supported by Microsoft.IdentityModel.Tokens in this project. Use HS256/384/512 or ES256/384/512.");
        }

        throw new InvalidOperationException($"Unsupported JWT algorithm '{options.Algorithm}'.");
    }

    private static JwtKeyMaterial CreateHmacMaterial(JwtOptions options, string algorithm)
    {
        var keyBytes = GetHmacKeyBytes(options);
        var minimumBytes = algorithm switch
        {
            SecurityAlgorithms.HmacSha256 => 32,
            SecurityAlgorithms.HmacSha384 => 48,
            SecurityAlgorithms.HmacSha512 => 64,
            _ => throw new InvalidOperationException($"Unsupported HMAC JWT algorithm '{algorithm}'.")
        };

        if (keyBytes.Length < minimumBytes)
            throw new InvalidOperationException($"Jwt:Secret must produce at least {minimumBytes} bytes for {algorithm}.");

        var key = new SymmetricSecurityKey(keyBytes);
        return new JwtKeyMaterial(new SigningCredentials(key, algorithm), key, algorithm);
    }

    private static JwtKeyMaterial CreateEcdsaMaterial(JwtOptions options, string algorithm)
    {
        var privatePem = ReadPem(options.PrivateKeyPem, options.PrivateKeyPath, "private");
        var publicPem = ReadPem(options.PublicKeyPem, options.PublicKeyPath, "public", required: false);

        var privateKey = ECDsa.Create();
        privateKey.ImportFromPem(privatePem);

        var validationKey = privateKey;
        if (!string.IsNullOrWhiteSpace(publicPem))
        {
            validationKey = ECDsa.Create();
            validationKey.ImportFromPem(publicPem);
        }

        var signingKey = new ECDsaSecurityKey(privateKey) { KeyId = "livefuelmap-jwt" };
        var publicKey = new ECDsaSecurityKey(validationKey) { KeyId = "livefuelmap-jwt" };
        return new JwtKeyMaterial(new SigningCredentials(signingKey, algorithm), publicKey, algorithm);
    }

    private static byte[] GetHmacKeyBytes(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
            throw new InvalidOperationException("Jwt:Secret is required.");

        var secret = options.Secret.Trim();
        if (!options.SecretIsBase64)
            return Encoding.UTF8.GetBytes(secret);

        try
        {
            return Convert.FromBase64String(secret);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Jwt:SecretIsBase64 is enabled, but Jwt:Secret is not valid Base64.", ex);
        }
    }

    private static string NormalizeAlgorithm(string? algorithm)
    {
        return (algorithm ?? "HS256").Trim().ToUpperInvariant() switch
        {
            "HS256" => SecurityAlgorithms.HmacSha256,
            "HS384" => SecurityAlgorithms.HmacSha384,
            "HS512" => SecurityAlgorithms.HmacSha512,
            "ES256" => SecurityAlgorithms.EcdsaSha256,
            "ES384" => SecurityAlgorithms.EcdsaSha384,
            "ES512" => SecurityAlgorithms.EcdsaSha512,
            "EDDSA" => "EdDSA",
            var value => value
        };
    }

    private static string ReadPem(string? inlinePem, string? path, string keyName, bool required = true)
    {
        if (!string.IsNullOrWhiteSpace(inlinePem))
            return inlinePem.Replace("\\n", "\n", StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(path))
        {
            var resolved = Path.GetFullPath(path);
            if (File.Exists(resolved))
                return File.ReadAllText(resolved);

            throw new InvalidOperationException($"JWT {keyName} key file does not exist: {resolved}");
        }

        if (!required)
            return string.Empty;

        throw new InvalidOperationException($"Jwt:{keyName} key PEM or path is required for ECDSA algorithms.");
    }
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public JwtAccessTokenDto CreateAccessToken(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64),
            new("role", user.Role.ToString()),
            new("token_version", user.TokenVersion.ToString())
        };

        var keyMaterial = JwtSigningKeyFactory.Create(_options);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: keyMaterial.SigningCredentials);

        return new JwtAccessTokenDto(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
