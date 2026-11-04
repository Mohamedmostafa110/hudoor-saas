namespace Hudoor.Api.Services;

public interface IRateLimiterService
{
    bool IsAccountLockedOut(string accountKey, out TimeSpan remainingLockout);
    bool IsIpRateLimited(string ipAddress);
    void RecordFailedAttempt(string accountKey, string ipAddress);
    void ResetAccount(string accountKey);
    Task ApplyProgressiveDelayAsync(string accountKey, CancellationToken cancellationToken = default);
}
