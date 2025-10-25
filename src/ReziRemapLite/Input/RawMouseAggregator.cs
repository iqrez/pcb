using System.Diagnostics;

namespace ReziRemapLite.Input;

public readonly record struct MouseSample(double DeltaX, double DeltaY, double DeltaTimeSeconds);

public sealed class RawMouseAggregator
{
    private readonly object _gate = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _pendingX;
    private double _pendingY;
    private double _lastTime;
    private bool _hasLast;

    public MouseSample Add(int deltaX, int deltaY)
    {
        lock (_gate)
        {
            _pendingX += deltaX;
            _pendingY += deltaY;
            var now = _stopwatch.Elapsed.TotalSeconds;
            double deltaTime;
            if (_hasLast)
            {
                deltaTime = now - _lastTime;
            }
            else
            {
                deltaTime = 1.0 / 1000.0;
                _hasLast = true;
            }

            _lastTime = now;
            var sample = new MouseSample(_pendingX, _pendingY, Math.Max(deltaTime, 1.0 / 1000.0));
            _pendingX = 0;
            _pendingY = 0;
            return sample;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _pendingX = 0;
            _pendingY = 0;
            _lastTime = _stopwatch.Elapsed.TotalSeconds;
            _hasLast = false;
        }
    }
}
