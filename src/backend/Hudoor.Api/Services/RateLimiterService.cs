using System.Collections.Concurrent;

namespace Hudoor.Api.Services;

public class RateLimiterService : IRateLimiterService
{
    private class AccountAttemptState
    {
        public int FailedAttempts { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public DateTime LastAttemptUtc { get; set; }
    }

    private class IpAttemptState
    {
        public Queue<DateTime> RequestTimestamps { get; } = new();
        public readonly object Lock = new();
    }

    private readonly ConcurrentDictionary<string, AccountAttemptState> _accountStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IpAttemptState> _ipStates = new(StringComparer.OrdinalIgnoreCase);

    private const int MaxAccountFailedAttempts = 5;
    private static readonly TimeSpan AccountLockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AccountWindowReset = TimeSpan.FromMinutes(15);

    // Classroom-aware IP volumetric protection: 100 requests per minute
    private const int MaxIpRequestsPerMinute = 100;
    private static readonly TimeSpan IpWindowDuration = TimeSpan.FromMinutes(1);

    public bool IsAccountLockedOut(string accountKey, out TimeSpan remainingLockout)
    {
        remainingLockout = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            return false;
        }

        if (_accountStates.TryGetValue(accountKey, out var state))
        {
            lock (state)
            {
                if (state.LockedUntilUtc.HasValue)
                {
                    var now = DateTime.UtcNow;
                    if (state.LockedUntilUtc.Value > now)
                    {
                        remainingLockout = state.LockedUntilUtc.Value - now;
                        return true;
                    }

                    // Lockout expired, reset state
                    state.LockedUntilUtc = null;
                    state.FailedAttempts = 0;
                }
            }
        }

        return false;
    }

    public bool IsIpRateLimited(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        var ipState = _ipStates.GetOrAdd(ipAddress, _ => new IpAttemptState());
        var now = DateTime.UtcNow;

        lock (ipState.Lock)
        {
            while (ipState.RequestTimestamps.Count > 0 && (now - ipState.RequestTimestamps.Peek()) > IpWindowDuration)
            {
                ipState.RequestTimestamps.Dequeue();
            }

            if (ipState.RequestTimestamps.Count >= MaxIpRequestsPerMinute)
            {
                return true;
            }

            ipState.RequestTimestamps.Enqueue(now);
            return false;
        }
    }

    public void RecordFailedAttempt(string accountKey, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var state = _accountStates.GetOrAdd(accountKey, _ => new AccountAttemptState());

        lock (state)
        {
            // If previous attempt was longer than window ago, reset count
            if (now - state.LastAttemptUtc > AccountWindowReset && !state.LockedUntilUtc.HasValue)
            {
                state.FailedAttempts = 0;
            }

            state.FailedAttempts++;
            state.LastAttemptUtc = now;

            if (state.FailedAttempts >= MaxAccountFailedAttempts)
            {
                state.LockedUntilUtc = now.Add(AccountLockoutDuration);
            }
        }
    }

    public void ResetAccount(string accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            return;
        }

        if (_accountStates.TryGetValue(accountKey, out var state))
        {
            lock (state)
            {
                state.FailedAttempts = 0;
                state.LockedUntilUtc = null;
            }
        }
    }

    public async Task ApplyProgressiveDelayAsync(string accountKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountKey))
        {
            return;
        }

        var attempts = 0;
        if (_accountStates.TryGetValue(accountKey, out var state))
        {
            lock (state)
            {
                attempts = state.FailedAttempts;
            }
        }

        if (attempts > 0)
        {
            // Progressive delay up to 1000ms
            var delayMs = Math.Min(attempts * 200, 1000);
            try
            {
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Request aborted
            }
        }
    }
}
