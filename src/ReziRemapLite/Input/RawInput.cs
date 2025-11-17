using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ReziRemapLite.Input;

public enum RawMouseEventType
{
    Move,
    ButtonDown,
    ButtonUp,
    Wheel,
}

public enum RawMouseButton
{
    Left,
    Right,
    Middle,
    XButton1,
    XButton2,
}

public sealed class RawMouseEventArgs : EventArgs
{
    public RawMouseEventArgs(RawMouseEventType type, RawMouseButton? button, int deltaX, int deltaY, int wheelDelta)
    {
        Type = type;
        Button = button;
        DeltaX = deltaX;
        DeltaY = deltaY;
        WheelDelta = wheelDelta;
    }

    public RawMouseEventType Type { get; }
    public RawMouseButton? Button { get; }
    public int DeltaX { get; }
    public int DeltaY { get; }
    public int WheelDelta { get; }
}

public sealed class RawKeyboardEventArgs : EventArgs
{
    public RawKeyboardEventArgs(Keys key, bool isDown)
    {
        Key = key;
        IsDown = isDown;
    }

    public Keys Key { get; }
    public bool IsDown { get; }
}

public sealed class RawInputMsgWindow : NativeWindow, IDisposable
{
    private const int WM_INPUT = 0x00FF;
    private const int RID_INPUT = 0x10000003;
    private const int RIDEV_INPUTSINK = 0x00000100;
    private const int HID_USAGE_PAGE_GENERIC = 0x01;
    private const int HID_USAGE_GENERIC_MOUSE = 0x02;
    private const int HID_USAGE_GENERIC_KEYBOARD = 0x06;

    public RawInputMsgWindow()
    {
        CreateHandle(new CreateParams
        {
            Caption = nameof(RawInputMsgWindow),
            Parent = new IntPtr(-3), // HWND_MESSAGE
        });

        RegisterForRawInput();
    }

    public event EventHandler<RawMouseEventArgs>? MouseInput;
    public event EventHandler<RawKeyboardEventArgs>? KeyboardInput;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            ProcessRawInput(m.LParam);
        }

        base.WndProc(ref m);
    }

    private void RegisterForRawInput()
    {
        var devices = new RAWINPUTDEVICE[]
        {
            new()
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_MOUSE,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = Handle,
            },
            new()
            {
                usUsagePage = HID_USAGE_PAGE_GENERIC,
                usUsage = HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = Handle,
            },
        };

        if (!NativeMethods.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register raw input devices.");
        }
    }

    private void ProcessRawInput(IntPtr lParam)
    {
        uint dwSize = 0;
        var headerSize = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        if (NativeMethods.GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize) != 0)
        {
            return;
        }

        if (dwSize == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)dwSize);
        try
        {
            if (NativeMethods.GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize) != dwSize)
            {
                return;
            }

            var raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
            if (raw.header.dwType == RawInputType.Mouse)
            {
                HandleMouse(raw.data.mouse);
            }
            else if (raw.header.dwType == RawInputType.Keyboard)
            {
                HandleKeyboard(raw.data.keyboard);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void HandleMouse(RAWMOUSE mouse)
    {
        if (mouse.lLastX != 0 || mouse.lLastY != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.Move, null, mouse.lLastX, mouse.lLastY, 0));
        }

        var buttonFlags = mouse.buttons.usButtonFlags;
        if (buttonFlags == 0)
        {
            return;
        }

        ProcessMouseButtonFlags(buttonFlags, mouse.buttons.usButtonData);
    }

    private void ProcessMouseButtonFlags(RawMouseButtonFlags buttonFlags, ushort buttonData)
    {
        if ((buttonFlags & RawMouseButtonFlags.LeftButtonDown) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonDown, RawMouseButton.Left, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.LeftButtonUp) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonUp, RawMouseButton.Left, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.RightButtonDown) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonDown, RawMouseButton.Right, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.RightButtonUp) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonUp, RawMouseButton.Right, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.MiddleButtonDown) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonDown, RawMouseButton.Middle, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.MiddleButtonUp) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonUp, RawMouseButton.Middle, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.Button4Down) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonDown, RawMouseButton.XButton1, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.Button4Up) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonUp, RawMouseButton.XButton1, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.Button5Down) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonDown, RawMouseButton.XButton2, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.Button5Up) != 0)
        {
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.ButtonUp, RawMouseButton.XButton2, 0, 0, 0));
        }
        if ((buttonFlags & RawMouseButtonFlags.MouseWheel) != 0)
        {
            var wheelDelta = (short)buttonData;
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.Wheel, null, 0, 0, wheelDelta));
        }
        if ((buttonFlags & RawMouseButtonFlags.MouseHWheel) != 0)
        {
            var wheelDelta = (short)buttonData;
            MouseInput?.Invoke(this, new RawMouseEventArgs(RawMouseEventType.Wheel, null, 0, 0, wheelDelta));
        }
    }

    private void HandleKeyboard(RAWKEYBOARD keyboard)
    {
        if (keyboard.VKey == 0)
        {
            return;
        }

        var isBreak = (keyboard.Flags & RawKeyboardFlags.Break) != 0;
        var virtualKey = (Keys)keyboard.VKey;

        if (virtualKey == Keys.ShiftKey)
        {
            var mapped = NativeMethods.MapVirtualKey((uint)keyboard.MakeCode, MapVirtualKeyMapTypes.MAPVK_VSC_TO_VK_EX);
            if (mapped == (uint)Keys.LShiftKey || mapped == (uint)Keys.RShiftKey)
            {
                virtualKey = (Keys)mapped;
            }
        }

        var args = new RawKeyboardEventArgs(virtualKey, !isBreak);
        KeyboardInput?.Invoke(this, args);
    }

    public void Dispose()
    {
        DestroyHandle();
        GC.SuppressFinalize(this);
    }

    private enum RawInputType : uint
    {
        Mouse = 0,
        Keyboard = 1,
        Hid = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public nint hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public RawInputType dwType;
        public uint dwSize;
        public nuint hDevice;
        public nuint wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public RawMouseFlags usFlags;
        public RawMouseButtonsUnion buttons;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RawMouseButtonsUnion
    {
        [FieldOffset(0)]
        public uint ulButtons;
        [FieldOffset(0)]
        public RawMouseButtonFlags usButtonFlags;
        [FieldOffset(2)]
        public ushort usButtonData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public RawKeyboardFlags Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWHID
    {
        public uint dwSizeHid;
        public uint dwCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWINPUTUNION data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RAWINPUTUNION
    {
        [FieldOffset(0)]
        public RAWMOUSE mouse;
        [FieldOffset(0)]
        public RAWKEYBOARD keyboard;
        [FieldOffset(0)]
        public RAWHID hid;
    }

    [Flags]
    private enum RawMouseFlags : ushort
    {
        MoveRelative = 0x0000,
        MoveAbsolute = 0x0001,
    }

    [Flags]
    private enum RawMouseButtonFlags : ushort
    {
        LeftButtonDown = 0x0001,
        LeftButtonUp = 0x0002,
        RightButtonDown = 0x0004,
        RightButtonUp = 0x0008,
        MiddleButtonDown = 0x0010,
        MiddleButtonUp = 0x0020,
        Button4Down = 0x0040,
        Button4Up = 0x0080,
        Button5Down = 0x0100,
        Button5Up = 0x0200,
        MouseWheel = 0x0400,
        MouseHWheel = 0x0800,
    }

    [Flags]
    private enum RawKeyboardFlags : ushort
    {
        Make = 0x00,
        Break = 0x01,
        E0 = 0x02,
        E1 = 0x04,
    }

    private enum MapVirtualKeyMapTypes : uint
    {
        MAPVK_VSC_TO_VK_EX = 3,
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, MapVirtualKeyMapTypes uMapType);
    }
}
