using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using ReziRemapLite.Config;
using ReziRemapLite.Input;
using ReziRemapLite.Output;

namespace ReziRemapLite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var context = new RemapApplicationContext();
        Application.Run(context);
    }
}

internal sealed class RemapApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _toggleMenuItem;
    private readonly VigemEngine _vigem = new();
    private readonly RawInputMsgWindow _rawInput = new();
    private readonly LowLevelMouseHook _mouseHook = new();
    private readonly RawMouseAggregator _mouseAggregator = new();
    private readonly MnKSettings _settings = MnKSettings.Default;
    private readonly CurveProcessor _curveProcessor;
    private readonly Dictionary<Keys, bool> _keyStates = new();
    private readonly object _stateGate = new();

    private bool _controllerMode = true;
    private bool _keyboardY;
    private bool _wheelPulseY;
    private System.Threading.Timer? _wheelTimer;
    private bool _disposed;
    private readonly bool _suppressMouseMove = true;

    public RemapApplicationContext()
    {
        _curveProcessor = new CurveProcessor(_settings);
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "ReziRemapLite",
        };
        _toggleMenuItem = new ToolStripMenuItem("Controller Mode: On");
        _toggleMenuItem.Click += (_, _) => ToggleControllerMode();
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => ExitThread();
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(exitMenuItem);
        _trayIcon.ContextMenuStrip = contextMenu;

        _trayIcon.BalloonTipTitle = "ReziRemapLite";

        try
        {
            _vigem.Start();
            _trayIcon.BalloonTipText = _vigem.IsReal ? "ViGEm connected." : "ViGEm unavailable.";
            _trayIcon.ShowBalloonTip(3000);
        }
        catch (Exception ex)
        {
            _trayIcon.BalloonTipText = $"ViGEm failed: {ex.Message}";
            _trayIcon.ShowBalloonTip(5000);
        }

        _rawInput.MouseInput += OnRawMouse;
        _rawInput.KeyboardInput += OnRawKeyboard;

        _mouseHook.MessageReceived += OnLowLevelMouseMessage;
        try
        {
            _mouseHook.Install();
        }
        catch (Exception ex)
        {
            _trayIcon.BalloonTipText = $"Hook failed: {ex.Message}";
            _trayIcon.ShowBalloonTip(5000);
        }

        UpdateTrayText();
        _curveProcessor.Reset();
        _mouseAggregator.Reset();
    }

    private void OnLowLevelMouseMessage(object? sender, LowLevelMouseHook.MouseMessageEventArgs e)
    {
        if (!_controllerMode)
        {
            return;
        }

        switch (e.Message)
        {
            case LowLevelMouseHook.MouseMessage.WM_MOUSEMOVE:
                e.Handled = _suppressMouseMove;
                break;
            case LowLevelMouseHook.MouseMessage.WM_LBUTTONDOWN:
            case LowLevelMouseHook.MouseMessage.WM_LBUTTONUP:
            case LowLevelMouseHook.MouseMessage.WM_RBUTTONDOWN:
            case LowLevelMouseHook.MouseMessage.WM_RBUTTONUP:
            case LowLevelMouseHook.MouseMessage.WM_MBUTTONDOWN:
            case LowLevelMouseHook.MouseMessage.WM_MBUTTONUP:
            case LowLevelMouseHook.MouseMessage.WM_MOUSEWHEEL:
            case LowLevelMouseHook.MouseMessage.WM_MOUSEHWHEEL:
            case LowLevelMouseHook.MouseMessage.WM_XBUTTONDOWN:
            case LowLevelMouseHook.MouseMessage.WM_XBUTTONUP:
                e.Handled = true;
                break;
        }
    }

    private void OnRawMouse(object? sender, RawMouseEventArgs e)
    {
        if (!_controllerMode)
        {
            return;
        }

        var needsSubmit = false;
        switch (e.Type)
        {
            case RawMouseEventType.Move:
            {
                var sample = _mouseAggregator.Add(e.DeltaX, e.DeltaY);
                var processed = _curveProcessor.Process(sample.DeltaX, sample.DeltaY, sample.DeltaTimeSeconds);
                var final = new Vector2(processed.X, -processed.Y);
                needsSubmit |= _vigem.TrySetRightStick(final.X, final.Y);
                break;
            }
            case RawMouseEventType.ButtonDown:
            case RawMouseEventType.ButtonUp:
            {
                var isDown = e.Type == RawMouseEventType.ButtonDown;
                switch (e.Button)
                {
                    case RawMouseButton.Left:
                        needsSubmit |= _vigem.TrySetRightTrigger(isDown ? 1f : 0f);
                        break;
                    case RawMouseButton.Right:
                        needsSubmit |= _vigem.TrySetLeftTrigger(isDown ? 1f : 0f);
                        break;
                    case RawMouseButton.Middle:
                        if (!isDown)
                        {
                            ToggleControllerMode();
                        }
                        break;
                    case RawMouseButton.XButton1:
                        needsSubmit |= _vigem.TrySetButton("LB", isDown);
                        break;
                    case RawMouseButton.XButton2:
                        needsSubmit |= _vigem.TrySetButton("RB", isDown);
                        break;
                }

                break;
            }
            case RawMouseEventType.Wheel:
            {
                if (e.WheelDelta != 0)
                {
                    TriggerWheelPulse();
                }

                break;
            }
        }

        if (needsSubmit)
        {
            _vigem.SubmitReport();
        }
    }

    private void OnRawKeyboard(object? sender, RawKeyboardEventArgs e)
    {
        lock (_stateGate)
        {
            _keyStates[e.Key] = e.IsDown;
        }

        if (e.Key == Keys.D1 || e.Key == Keys.D0)
        {
            CheckSafetyChord();
        }

        if (!_controllerMode)
        {
            return;
        }

        var needsSubmit = false;
        var isDown = e.IsDown;
        switch (e.Key)
        {
            case Keys.Escape:
                needsSubmit |= _vigem.TrySetButton("Start", isDown);
                break;
            case Keys.Tab:
                needsSubmit |= _vigem.TrySetButton("Back", isDown);
                break;
            case Keys.Space:
                needsSubmit |= _vigem.TrySetButton("A", isDown);
                break;
            case Keys.L:
            case Keys.LShiftKey:
            case Keys.RShiftKey:
            case Keys.ShiftKey:
                needsSubmit |= _vigem.TrySetButton("B", isDown);
                break;
            case Keys.F:
                needsSubmit |= _vigem.TrySetButton("X", isDown);
                break;
            case Keys.D1:
                needsSubmit |= _vigem.TrySetButton("Down", isDown);
                break;
            case Keys.D2:
                needsSubmit |= _vigem.TrySetButton("Left", isDown);
                break;
            case Keys.D3:
                needsSubmit |= _vigem.TrySetButton("Right", isDown);
                break;
            case Keys.Q:
                needsSubmit |= _vigem.TrySetButton("LeftThumb", isDown);
                break;
            case Keys.E:
                needsSubmit |= _vigem.TrySetButton("RightThumb", isDown);
                break;
            case Keys.R:
                _keyboardY = isDown;
                needsSubmit |= UpdateYButton();
                break;
        }

        if (IsWASDKey(e.Key))
        {
            needsSubmit |= UpdateLeftStick();
        }

        if (needsSubmit)
        {
            _vigem.SubmitReport();
        }
    }

    private bool UpdateLeftStick()
    {
        if (!_controllerMode)
        {
            return false;
        }

        var (w, a, s, d) = GetWasdState();
        var x = (d ? 1f : 0f) - (a ? 1f : 0f);
        var y = (w ? 1f : 0f) - (s ? 1f : 0f);
        var vector = new Vector2(x, y);
        if (vector.LengthSquared() > 1f)
        {
            vector = Vector2.Normalize(vector);
        }

        return _vigem.TrySetLeftStick(vector.X, vector.Y);
    }

    private bool UpdateYButton()
    {
        if (!_controllerMode)
        {
            return false;
        }

        var desired = _keyboardY || _wheelPulseY;
        return _vigem.TrySetButton("Y", desired);
    }

    private void TriggerWheelPulse()
    {
        _wheelTimer?.Dispose();
        _wheelPulseY = true;
        if (UpdateYButton())
        {
            _vigem.SubmitReport();
        }

        _wheelTimer = new System.Threading.Timer(_ =>
        {
            _wheelPulseY = false;
            if (UpdateYButton())
            {
                _vigem.SubmitReport();
            }
        }, null, 50, System.Threading.Timeout.Infinite);
    }

    private void CheckSafetyChord()
    {
        if (IsKeyDown(Keys.D1) && IsKeyDown(Keys.D0))
        {
            ExitThread();
        }
    }

    private bool IsKeyDown(Keys key)
    {
        lock (_stateGate)
        {
            return _keyStates.TryGetValue(key, out var value) && value;
        }
    }

    private static bool IsWASDKey(Keys key) => key is Keys.W or Keys.A or Keys.S or Keys.D;

    private (bool W, bool A, bool S, bool D) GetWasdState()
    {
        lock (_stateGate)
        {
            return (IsKeyDownInternal(Keys.W), IsKeyDownInternal(Keys.A), IsKeyDownInternal(Keys.S), IsKeyDownInternal(Keys.D));
        }

        bool IsKeyDownInternal(Keys key) => _keyStates.TryGetValue(key, out var value) && value;
    }

    private void ToggleControllerMode()
    {
        _controllerMode = !_controllerMode;
        UpdateTrayText();
        _trayIcon.ShowBalloonTip(1000, "ReziRemapLite", _controllerMode ? "Mode: Controller" : "Mode: Mouse+KB", ToolTipIcon.Info);

        if (!_controllerMode)
        {
            _keyboardY = false;
            _wheelPulseY = false;
            _wheelTimer?.Dispose();
            _wheelTimer = null;
            _vigem.Reset();
            _vigem.SubmitReport();
        }
        else
        {
            _curveProcessor.Reset();
            _mouseAggregator.Reset();
        }
    }

    private void UpdateTrayText()
    {
        _toggleMenuItem.Text = _controllerMode ? "Controller Mode: On" : "Controller Mode: Off";
        _trayIcon.Text = _controllerMode ? "ReziRemapLite - Controller" : "ReziRemapLite - Mouse";
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _wheelTimer?.Dispose();
            _wheelTimer = null;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _mouseHook.Dispose();
            _rawInput.Dispose();
            _vigem.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}
