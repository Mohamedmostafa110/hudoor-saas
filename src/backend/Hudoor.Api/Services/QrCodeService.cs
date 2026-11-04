using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Hudoor.Api.Services;

public class QrCodeService : IQrCodeService
{
    private const int IntervalSeconds = 20;
    private const int MaxStepDifference = 1; // Allows current step ± 1 step (up to 40 seconds skew)
    private readonly byte[] _signingKey;

    public QrCodeService(IConfiguration configuration)
    {
        var keyString = configuration["QrCode:SigningKey"] 
            ?? configuration["Jwt:Key"] 
            ?? throw new InvalidOperationException("QR Code signing key is not configured.");

        _signingKey = Encoding.UTF8.GetBytes(keyString);
    }

    public (string Token, int RemainingSeconds, DateTime ExpiresAtUtc) GenerateToken(
        Guid sessionId,
        string sessionSalt,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var unixSeconds = new DateTimeOffset(now).ToUnixTimeSeconds();
        var step = unixSeconds / IntervalSeconds;

        var elapsedInStep = (int)(unixSeconds % IntervalSeconds);
        var remainingSeconds = IntervalSeconds - elapsedInStep;
        var expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds((step + 1) * IntervalSeconds).UtcDateTime;

        var payloadToSign = $"{sessionId:D}:{step}:{sessionSalt}";
        var signatureBytes = ComputeHmacSha256(payloadToSign);
        var signatureHex = Convert.ToHexString(signatureBytes);

        var token = $"{sessionId:N}.{step}.{signatureHex}";
        return (token, remainingSeconds, expiresAtUtc);
    }

    public Guid? ExtractSessionIdFromToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        if (Guid.TryParseExact(parts[0], "N", out var sessionId))
        {
            return sessionId;
        }

        if (Guid.TryParse(parts[0], out sessionId))
        {
            return sessionId;
        }

        return null;
    }

    public (bool IsValid, string? ErrorMessage) ValidateToken(
        string token,
        Guid expectedSessionId,
        string sessionSalt,
        DateTime? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "QR attendance token is required.");
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return (false, "Invalid QR token format.");
        }

        if (!Guid.TryParseExact(parts[0], "N", out var tokenSessionId) &&
            !Guid.TryParse(parts[0], out tokenSessionId))
        {
            return (false, "Invalid session identifier in QR token.");
        }

        if (tokenSessionId != expectedSessionId)
        {
            return (false, "QR token does not belong to this attendance session.");
        }

        if (!long.TryParse(parts[1], out var tokenStep))
        {
            return (false, "Invalid timestamp in QR token.");
        }

        byte[] providedSignatureBytes;
        try
        {
            providedSignatureBytes = Convert.FromHexString(parts[2]);
        }
        catch (FormatException)
        {
            return (false, "Invalid cryptographic signature format in QR token.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var currentUnixSeconds = new DateTimeOffset(now).ToUnixTimeSeconds();
        var currentStep = currentUnixSeconds / IntervalSeconds;

        var stepDifference = Math.Abs(currentStep - tokenStep);
        if (stepDifference > MaxStepDifference)
        {
            return (false, "QR token has expired. Please scan the current rotating QR code displayed by the teacher.");
        }

        // Recompute expected HMAC for the token's specific step
        var payloadToVerify = $"{expectedSessionId:D}:{tokenStep}:{sessionSalt}";
        var expectedSignatureBytes = ComputeHmacSha256(payloadToVerify);

        if (!CryptographicOperations.FixedTimeEquals(expectedSignatureBytes, providedSignatureBytes))
        {
            return (false, "QR token signature verification failed. Token is invalid or has been altered.");
        }

        return (true, null);
    }

    private byte[] ComputeHmacSha256(string data)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
}
