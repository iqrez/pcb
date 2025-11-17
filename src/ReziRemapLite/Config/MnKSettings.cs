namespace ReziRemapLite.Config;

public sealed class MnKSettings
{
    public double Sensitivity { get; init; } = 1.0;
    public double Dpi { get; init; } = 1600.0;
    public double Deadzone { get; init; } = 0.05;
    public double AntiDeadzone { get; init; } = 0.10;
    public double YxRatio { get; init; } = 1.0;
    public double SmoothingMs { get; init; } = 0.0;
    public double AdaptiveThresholdCps { get; init; } = 250.0;
    public bool QuantizeForAA { get; init; } = false;
    public int QuantizeLevels { get; init; } = 32;
    public double FullDeflectionCps { get; init; } = 2500.0;

    public static MnKSettings Default { get; } = new();
}
