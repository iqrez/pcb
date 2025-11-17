using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReziRemapLite.Input;

public sealed class LowLevelMouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;

    private readonly HookProc _proc;
    private IntPtr _hookId;

    public LowLevelMouseHook()
    {
        _proc = HookCallback;
    }

    public event EventHandler<MouseMessageEventArgs>? MessageReceived;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule?.ModuleName is string name ? NativeMethods.GetModuleHandle(name) : IntPtr.Zero;
        _hookId = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, _proc, moduleHandle, 0);
        if (_hookId == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install low level mouse hook.");
        }
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (MouseMessage)wParam;
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            var args = new MouseMessageEventArgs(message, data);
            MessageReceived?.Invoke(this, args);
            if (args.Handled)
            {
                return new IntPtr(1);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Uninstall();
        GC.SuppressFinalize(this);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    public enum MouseMessage : int
    {
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208,
        WM_MOUSEWHEEL = 0x020A,
        WM_XBUTTONDOWN = 0x020B,
        WM_XBUTTONUP = 0x020C,
        WM_MOUSEHWHEEL = 0x020E,
    }

    public sealed class MouseMessageEventArgs : EventArgs
    {
        internal MouseMessageEventArgs(MouseMessage message, NativeMethods.MSLLHOOKSTRUCT data)
        {
            Message = message;
            Data = data;
        }

        public MouseMessage Message { get; }
        public NativeMethods.MSLLHOOKSTRUCT Data { get; }
        public bool Handled { get; set; }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public nuint dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);
    }
}
