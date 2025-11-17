using System.ComponentModel;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ReziRemapLite.Output;

public sealed class VigemEngine : IDisposable
{
    private ViGEmClient? _client;
    private Xbox360Controller? _controller;
    private Xbox360Report _report;
    private bool _isConnected;
    private bool _isReal;

    private bool _dpadUp;
    private bool _dpadDown;
    private bool _dpadLeft;
    private bool _dpadRight;

    public bool IsConnected => _isConnected;
    public bool IsReal => _isReal;

    public void Start()
    {
        if (_isConnected)
        {
            return;
        }

        try
        {
            _client = new ViGEmClient();
            _controller = new Xbox360Controller(_client)
            {
                AutoSubmitReport = false,
            };
            _controller.Connect();
            _report = new Xbox360Report();
            _isReal = true;
            _isConnected = true;
        }
        catch (VigemBusNotFoundException)
        {
            _isReal = false;
            _isConnected = false;
            throw;
        }
        catch (Exception ex) when (ex is Win32Exception or VigemAllocFailedException)
        {
            _isReal = false;
            _isConnected = false;
            throw;
        }
    }

    public void SubmitReport()
    {
        if (_controller is null)
        {
            return;
        }

        _controller.SendReport(_report);
    }

    public bool TrySetRightStick(float x, float y)
    {
        if (_controller is null)
        {
            return false;
        }

        _report.SetAxis(Xbox360Axis.RightThumbX, ConvertStick(x));
        _report.SetAxis(Xbox360Axis.RightThumbY, ConvertStick(y));
        return true;
    }

    public bool TrySetLeftStick(float x, float y)
    {
        if (_controller is null)
        {
            return false;
        }

        _report.SetAxis(Xbox360Axis.LeftThumbX, ConvertStick(x));
        _report.SetAxis(Xbox360Axis.LeftThumbY, ConvertStick(y));
        return true;
    }

    public bool TrySetLeftTrigger(float value)
    {
        if (_controller is null)
        {
            return false;
        }

        _report.SetAxis(Xbox360Axis.LeftTrigger, ConvertTrigger(value));
        return true;
    }

    public bool TrySetRightTrigger(float value)
    {
        if (_controller is null)
        {
            return false;
        }

        _report.SetAxis(Xbox360Axis.RightTrigger, ConvertTrigger(value));
        return true;
    }

    public bool TrySetButton(string logical, bool down)
    {
        if (_controller is null)
        {
            return false;
        }

        switch (logical)
        {
            case "A":
                _report.SetButtonState(Xbox360Button.A, down);
                return true;
            case "B":
                _report.SetButtonState(Xbox360Button.B, down);
                return true;
            case "X":
                _report.SetButtonState(Xbox360Button.X, down);
                return true;
            case "Y":
                _report.SetButtonState(Xbox360Button.Y, down);
                return true;
            case "Start":
                _report.SetButtonState(Xbox360Button.Start, down);
                return true;
            case "Back":
                _report.SetButtonState(Xbox360Button.Back, down);
                return true;
            case "LB":
                _report.SetButtonState(Xbox360Button.LeftShoulder, down);
                return true;
            case "RB":
                _report.SetButtonState(Xbox360Button.RightShoulder, down);
                return true;
            case "LeftThumb":
                _report.SetButtonState(Xbox360Button.LeftThumb, down);
                return true;
            case "RightThumb":
                _report.SetButtonState(Xbox360Button.RightThumb, down);
                return true;
            case "Up":
                _dpadUp = down;
                ApplyDpad();
                return true;
            case "Down":
                _dpadDown = down;
                ApplyDpad();
                return true;
            case "Left":
                _dpadLeft = down;
                ApplyDpad();
                return true;
            case "Right":
                _dpadRight = down;
                ApplyDpad();
                return true;
            default:
                return false;
        }
    }

    public void Dispose()
    {
        if (_controller is not null)
        {
            _controller.Disconnect();
            _controller.Dispose();
        }

        _client?.Dispose();
        _controller = null;
        _client = null;
        _isConnected = false;
        GC.SuppressFinalize(this);
    }

    public void Reset()
    {
        _dpadUp = false;
        _dpadDown = false;
        _dpadLeft = false;
        _dpadRight = false;
        TrySetLeftStick(0, 0);
        TrySetRightStick(0, 0);
        TrySetLeftTrigger(0);
        TrySetRightTrigger(0);
        TrySetButton("A", false);
        TrySetButton("B", false);
        TrySetButton("X", false);
        TrySetButton("Y", false);
        TrySetButton("Start", false);
        TrySetButton("Back", false);
        TrySetButton("LB", false);
        TrySetButton("RB", false);
        TrySetButton("LeftThumb", false);
        TrySetButton("RightThumb", false);
        TrySetButton("Up", false);
        TrySetButton("Down", false);
        TrySetButton("Left", false);
        TrySetButton("Right", false);
    }

    private static short ConvertStick(float value)
    {
        var clamped = Math.Clamp(value, -1f, 1f);
        if (clamped >= 0)
        {
            return (short)Math.Round(clamped * short.MaxValue);
        }

        return (short)Math.Round(clamped * -short.MinValue);
    }

    private static byte ConvertTrigger(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        return (byte)Math.Round(clamped * byte.MaxValue);
    }

    private void ApplyDpad()
    {
        if (_controller is null)
        {
            return;
        }

        var direction = Xbox360DPadDirection.None;
        if (_dpadUp && _dpadRight)
        {
            direction = Xbox360DPadDirection.NorthEast;
        }
        else if (_dpadUp && _dpadLeft)
        {
            direction = Xbox360DPadDirection.NorthWest;
        }
        else if (_dpadDown && _dpadRight)
        {
            direction = Xbox360DPadDirection.SouthEast;
        }
        else if (_dpadDown && _dpadLeft)
        {
            direction = Xbox360DPadDirection.SouthWest;
        }
        else if (_dpadUp)
        {
            direction = Xbox360DPadDirection.North;
        }
        else if (_dpadDown)
        {
            direction = Xbox360DPadDirection.South;
        }
        else if (_dpadLeft)
        {
            direction = Xbox360DPadDirection.West;
        }
        else if (_dpadRight)
        {
            direction = Xbox360DPadDirection.East;
        }

        _report.SetDPad(direction);
    }
}
