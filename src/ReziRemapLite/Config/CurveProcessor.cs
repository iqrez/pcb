using System.Numerics;

namespace ReziRemapLite.Config;

public sealed class CurveProcessor
{
    private readonly MnKSettings _settings;
    private Vector2 _ema;
    private bool _hasEma;

    public CurveProcessor(MnKSettings settings)
    {
        _settings = settings;
    }

    public Vector2 Process(double deltaX, double deltaY, double deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0)
        {
            return Vector2.Zero;
        }

        var sensitivity = (float)_settings.Sensitivity;
        var vx = (float)((deltaX / deltaTimeSeconds) * sensitivity);
        var vy = (float)((deltaY / deltaTimeSeconds) * sensitivity * _settings.YxRatio);

        var velocity = new Vector2(vx, vy);
        var speed = velocity.Length();

        if (_settings.SmoothingMs > 0 && speed < _settings.AdaptiveThresholdCps)
        {
            var smoothingSeconds = (float)(_settings.SmoothingMs / 1000.0);
            var alpha = (float)(deltaTimeSeconds / (smoothingSeconds + deltaTimeSeconds));
            if (!_hasEma)
            {
                _ema = velocity;
                _hasEma = true;
            }
            else
            {
                _ema = Vector2.Lerp(_ema, velocity, alpha);
            }

            velocity = _ema;
            speed = velocity.Length();
        }
        else
        {
            _ema = velocity;
            _hasEma = true;
        }

        if (speed <= float.Epsilon)
        {
            return Vector2.Zero;
        }

        var normalized = velocity / Math.Max(speed, float.Epsilon);
        var fullDeflection = Math.Max((float)_settings.FullDeflectionCps, 1f);
        var magnitude = MathF.Min(speed / fullDeflection, 1f);

        if (magnitude <= _settings.Deadzone)
        {
            return Vector2.Zero;
        }

        var remainder = (magnitude - (float)_settings.Deadzone) / MathF.Max(1f - (float)_settings.Deadzone, 1e-5f);
        var scaledMagnitude = (float)_settings.AntiDeadzone + remainder * (1f - (float)_settings.AntiDeadzone);
        scaledMagnitude = Math.Clamp(scaledMagnitude, (float)_settings.AntiDeadzone, 1f);

        var output = normalized * scaledMagnitude;

        if (_settings.QuantizeForAA && _settings.QuantizeLevels > 1)
        {
            var stepCount = _settings.QuantizeLevels - 1;
            var step = 2f / stepCount;
            output.X = Quantize(output.X, step);
            output.Y = Quantize(output.Y, step);
        }

        output.X = Math.Clamp(output.X, -1f, 1f);
        output.Y = Math.Clamp(output.Y, -1f, 1f);

        return output;
    }

    public void Reset()
    {
        _hasEma = false;
        _ema = Vector2.Zero;
    }

    private static float Quantize(float value, float step)
    {
        var normalized = (value + 1f) / step;
        var quantized = MathF.Round(normalized) * step - 1f;
        return Math.Clamp(quantized, -1f, 1f);
    }
}
