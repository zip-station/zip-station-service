using System.Collections.Concurrent;

namespace ZipStation.Business.Services;

public interface IRateLimiter
{
    bool TryAcquire(string key, TimeSpan cooldown, out TimeSpan retryAfter);
}

public class InMemoryRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, long> _lastAcquired = new();

    public bool TryAcquire(string key, TimeSpan cooldown, out TimeSpan retryAfter)
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var cooldownTicks = cooldown.Ticks;

        while (true)
        {
            var existing = _lastAcquired.TryGetValue(key, out var t) ? t : 0L;
            var elapsed = nowTicks - existing;
            if (elapsed < cooldownTicks)
            {
                retryAfter = TimeSpan.FromTicks(cooldownTicks - elapsed);
                return false;
            }

            if (existing == 0L)
            {
                if (_lastAcquired.TryAdd(key, nowTicks))
                {
                    retryAfter = TimeSpan.Zero;
                    return true;
                }
            }
            else if (_lastAcquired.TryUpdate(key, nowTicks, existing))
            {
                retryAfter = TimeSpan.Zero;
                return true;
            }
        }
    }
}
