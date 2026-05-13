using System.Collections.Concurrent;
using DirectorySelfService.Models;
using DirectorySelfService.Options;
using Microsoft.Extensions.Options;

namespace DirectorySelfService.Services;

public sealed class RateLimitService(IOptions<RateLimitOptions> options, TimeProvider timeProvider) : IRateLimitService
{
    private readonly RateLimitOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, AttemptWindow> _attempts = new();

    public PasswordChangeResult? Check(string sourceIp, string username)
    {
        CleanupExpired();
        var now = timeProvider.GetUtcNow();
        var ipKey = BuildKey("ip", sourceIp);
        var userKey = BuildKey("user", username.Trim().ToLowerInvariant());

        if (IsLimited(ipKey, _options.PermitLimit, now) || IsLimited(userKey, _options.UsernamePermitLimit, now))
        {
            return PasswordChangeResult.Fail(PasswordChangeResultCategory.RateLimited, "Too many unsuccessful attempts. Wait before trying again.");
        }

        return null;
    }

    public void RecordFailure(string sourceIp, string username)
    {
        var now = timeProvider.GetUtcNow();
        Increment(BuildKey("ip", sourceIp), now);
        Increment(BuildKey("user", username.Trim().ToLowerInvariant()), now);
    }

    public void RecordSuccess(string sourceIp, string username)
    {
        _attempts.TryRemove(BuildKey("ip", sourceIp), out _);
        _attempts.TryRemove(BuildKey("user", username.Trim().ToLowerInvariant()), out _);
    }

    private bool IsLimited(string key, int limit, DateTimeOffset now) =>
        _attempts.TryGetValue(key, out var window) && window.ExpiresAt > now && window.Count >= limit;

    private void Increment(string key, DateTimeOffset now)
    {
        _attempts.AddOrUpdate(key,
            _ => new AttemptWindow(1, now.AddMinutes(_options.WindowMinutes)),
            (_, existing) => existing.ExpiresAt <= now
                ? new AttemptWindow(1, now.AddMinutes(_options.WindowMinutes))
                : existing with { Count = existing.Count + 1 });
    }

    private void CleanupExpired()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var pair in _attempts)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _attempts.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string BuildKey(string scope, string value) => $"{scope}:{value}";

    private sealed record AttemptWindow(int Count, DateTimeOffset ExpiresAt);
}
