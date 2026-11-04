using Hudoor.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Hudoor.Api.Tests.Unit;

public class QrCodeServiceTests
{
    private readonly IQrCodeService _service;

    public QrCodeServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "SuperSecretKeyForQrCodeTestingPurpose2026!MustBeAtLeast32Bytes"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _service = new QrCodeService(configuration);
    }

    [Fact]
    public void GenerateToken_ReturnsValidTokenFormatAndMetadata()
    {
        var sessionId = Guid.NewGuid();
        var salt = "random_salt_1234567890";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 5, DateTimeKind.Utc); // 5s into 20s window

        var (token, remainingSeconds, expiresAtUtc) = _service.GenerateToken(sessionId, salt, testTime);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(15, remainingSeconds); // 20 - 5 = 15
        Assert.Equal(new DateTime(2026, 11, 3, 10, 0, 20, DateTimeKind.Utc), expiresAtUtc);

        var extractedId = _service.ExtractSessionIdFromToken(token);
        Assert.Equal(sessionId, extractedId);
    }

    [Fact]
    public void ValidateToken_CurrentStep_Succeeds()
    {
        var sessionId = Guid.NewGuid();
        var salt = "secure_session_salt_abc";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 5, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt, testTime);

        // Validate within the same window (e.g. 8s into window)
        var validateTime = new DateTime(2026, 11, 3, 10, 0, 8, DateTimeKind.Utc);
        var (isValid, error) = _service.ValidateToken(token, sessionId, salt, validateTime);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateToken_PreviousStepWithinTolerance_Succeeds()
    {
        var sessionId = Guid.NewGuid();
        var salt = "secure_session_salt_abc";
        // Generated at second 18 of minute 0 (step = T/20)
        var testTime = new DateTime(2026, 11, 3, 10, 0, 18, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt, testTime);

        // Validated at second 25 of minute 0 (next step, step diff = 1, age = 7 seconds)
        var validateTime = new DateTime(2026, 11, 3, 10, 0, 25, DateTimeKind.Utc);
        var (isValid, error) = _service.ValidateToken(token, sessionId, salt, validateTime);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateToken_ExpiredBeyondTolerance_Fails()
    {
        var sessionId = Guid.NewGuid();
        var salt = "secure_session_salt_abc";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt, testTime);

        // Validated 45 seconds later (step diff >= 2)
        var validateTime = new DateTime(2026, 11, 3, 10, 0, 45, DateTimeKind.Utc);
        var (isValid, error) = _service.ValidateToken(token, sessionId, salt, validateTime);

        Assert.False(isValid);
        Assert.Contains("expired", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateToken_WrongSessionId_Fails()
    {
        var sessionId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var salt = "salt";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt, testTime);

        var (isValid, error) = _service.ValidateToken(token, otherSessionId, salt, testTime);

        Assert.False(isValid);
        Assert.Contains("does not belong", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateToken_TamperedSignature_Fails()
    {
        var sessionId = Guid.NewGuid();
        var salt = "salt";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt, testTime);
        var parts = token.Split('.');
        var tamperedToken = $"{parts[0]}.{parts[1]}.{parts[2][..^2]}00"; // alter last bytes

        var (isValid, error) = _service.ValidateToken(tamperedToken, sessionId, salt, testTime);

        Assert.False(isValid);
        Assert.Contains("verification failed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateToken_WrongSalt_Fails()
    {
        var sessionId = Guid.NewGuid();
        var salt1 = "salt_one";
        var salt2 = "salt_two";
        var testTime = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

        var (token, _, _) = _service.GenerateToken(sessionId, salt1, testTime);

        var (isValid, error) = _service.ValidateToken(token, sessionId, salt2, testTime);

        Assert.False(isValid);
        Assert.Contains("verification failed", error, StringComparison.OrdinalIgnoreCase);
    }
}
