namespace CleanChannel.Landing;

public sealed class DemoAuthRateLimiter
{
    private const string OverflowKey = "__overflow__";
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly int _permitLimit;
    private readonly TimeSpan _window;
    private readonly int _maxTrackedKeys;
    private readonly Dictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private Window? _overflowWindow;

    public DemoAuthRateLimiter(LandingOptions options, TimeProvider clock)
    {
        _clock = clock;
        _permitLimit = options.AuthRateLimitPermitLimit;
        _window = TimeSpan.FromSeconds(options.AuthRateLimitWindowSeconds);
        _maxTrackedKeys = options.AuthRateLimitMaxTrackedKeys;
    }

    public bool TryAccept(string? clientKey)
    {
        var key = string.IsNullOrWhiteSpace(clientKey) ? OverflowKey : clientKey.Trim();
        if (key.Length > 128)
        {
            key = OverflowKey;
        }

        lock (_gate)
        {
            if (!_windows.ContainsKey(key) && _windows.Count >= _maxTrackedKeys)
            {
                return TryAcceptOverflow(_clock.GetUtcNow());
            }

            var now = _clock.GetUtcNow();
            if (!_windows.TryGetValue(key, out var window) || now - window.StartedAt >= _window)
            {
                _windows[key] = new Window(now, 1);
                return true;
            }

            if (window.Count >= _permitLimit)
            {
                return false;
            }

            _windows[key] = window with { Count = window.Count + 1 };
            return true;
        }
    }

    private bool TryAcceptOverflow(DateTimeOffset now)
    {
        if (_overflowWindow is null || now - _overflowWindow.StartedAt >= _window)
        {
            _overflowWindow = new Window(now, 1);
            return true;
        }

        if (_overflowWindow.Count >= _permitLimit)
        {
            return false;
        }

        _overflowWindow = _overflowWindow with { Count = _overflowWindow.Count + 1 };
        return true;
    }

    private sealed record Window(DateTimeOffset StartedAt, int Count);
}
