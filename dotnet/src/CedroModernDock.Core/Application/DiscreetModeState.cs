namespace CedroModernDock.Core.Application;

/// <summary>Tracks uninterrupted idle time without depending on UI timers or the OS.</summary>
public sealed class DiscreetModeState
{
    private TimeSpan? _idleSince;

    public bool Update(bool enabled, bool interacting, int delayMilliseconds, TimeSpan now)
    {
        if (!enabled || interacting)
        {
            Reset();
            return false;
        }
        _idleSince ??= now;
        return now - _idleSince.Value >= TimeSpan.FromMilliseconds(Math.Clamp(delayMilliseconds, 0, 1000));
    }

    public void Reset() => _idleSince = null;
}
