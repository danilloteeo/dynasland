namespace Dynasland.Modules.Timer;

/// <summary>Cronômetro baseado em instantes (não acumula erro de timers). Não é thread-safe.</summary>
public sealed class StopwatchEngine(TimeProvider time)
{
    private TimeSpan _accumulated;
    private DateTimeOffset? _runningSince;

    public bool IsActive { get; private set; }

    public bool IsRunning => _runningSince is not null;

    public TimeSpan Elapsed => _accumulated + (_runningSince is { } since ? time.GetUtcNow() - since : TimeSpan.Zero);

    public void Start()
    {
        _accumulated = TimeSpan.Zero;
        _runningSince = time.GetUtcNow();
        IsActive = true;
    }

    public void Pause()
    {
        if (IsRunning)
        {
            _accumulated = Elapsed;
            _runningSince = null;
        }
    }

    public void Resume()
    {
        if (IsActive && !IsRunning)
        {
            _runningSince = time.GetUtcNow();
        }
    }

    public void Reset()
    {
        _accumulated = TimeSpan.Zero;
        _runningSince = null;
        IsActive = false;
    }
}

/// <summary>Temporizador regressivo baseado no instante de término. Não é thread-safe.</summary>
public sealed class CountdownEngine(TimeProvider time)
{
    private DateTimeOffset? _endsAt;
    private TimeSpan _remainingWhilePaused;

    public TimeSpan Duration { get; private set; }

    public bool IsActive { get; private set; }

    public bool IsRunning => _endsAt is not null;

    public TimeSpan Remaining
    {
        get
        {
            if (!IsActive)
            {
                return TimeSpan.Zero;
            }

            if (_endsAt is { } endsAt)
            {
                var remaining = endsAt - time.GetUtcNow();
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }

            return _remainingWhilePaused;
        }
    }

    public bool IsFinished => IsActive && Remaining == TimeSpan.Zero;

    /// <summary>Fração já decorrida (0 a 1).</summary>
    public double Progress => Duration > TimeSpan.Zero ? Math.Clamp(1 - Remaining / Duration, 0, 1) : 0;

    public void Start(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        Duration = duration;
        _endsAt = time.GetUtcNow() + duration;
        IsActive = true;
    }

    public void Pause()
    {
        if (IsRunning)
        {
            _remainingWhilePaused = Remaining;
            _endsAt = null;
        }
    }

    public void Resume()
    {
        if (IsActive && !IsRunning)
        {
            _endsAt = time.GetUtcNow() + _remainingWhilePaused;
        }
    }

    public void Add(TimeSpan extra)
    {
        if (!IsActive)
        {
            return;
        }

        Duration += extra;
        if (_endsAt is { } endsAt)
        {
            _endsAt = endsAt + extra;
        }
        else
        {
            _remainingWhilePaused += extra;
        }
    }

    public void Cancel()
    {
        IsActive = false;
        _endsAt = null;
        _remainingWhilePaused = TimeSpan.Zero;
    }
}
