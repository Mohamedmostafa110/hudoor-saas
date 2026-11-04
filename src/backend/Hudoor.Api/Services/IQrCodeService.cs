namespace Hudoor.Api.Services;

public interface IQrCodeService
{
    /// <summary>
    /// Generates a dynamic, time-limited cryptographic QR attendance token for a session.
    /// Rotates every 20 seconds.
    /// </summary>
    (string Token, int RemainingSeconds, DateTime ExpiresAtUtc) GenerateToken(
        Guid sessionId,
        string sessionSalt,
        DateTime? utcNow = null);

    /// <summary>
    /// Extracts the SessionId from a QR token without full cryptographic validation.
    /// Used to look up the session and its specific salt before full validation.
    /// </summary>
    Guid? ExtractSessionIdFromToken(string token);

    /// <summary>
    /// Validates the authenticity, integrity, and time freshness of a QR attendance token.
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidateToken(
        string token,
        Guid expectedSessionId,
        string sessionSalt,
        DateTime? utcNow = null);
}
