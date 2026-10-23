using System;
using System.Runtime.InteropServices;

namespace ClipPeek
{
    internal static class Native
    {
        [DllImport("user32.dll")]
        internal static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("shell32.dll")]
        internal static extern int SHQueryUserNotificationState(out int state);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT placement);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WM_HOTKEY = 0x0312;
        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_NOREPEAT = 0x4000;
        internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        internal static void MakeToolWindow(IntPtr handle)
        {
            int ex = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        }

        internal static void MakeNoActivate(IntPtr handle)
        {
            int ex = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        internal static bool ForegroundCoversScreen()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;

            RECT r;
            if (!GetWindowRect(fg, out r)) return false;

            IntPtr mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(mon, ref mi)) return false;

            return r.Left <= mi.rcMonitor.Left
                && r.Top <= mi.rcMonitor.Top
                && r.Right >= mi.rcMonitor.Right
                && r.Bottom >= mi.rcMonitor.Bottom;
        }

        // 只认「真全屏 / 演示模式」：3 = D3D 全屏，4 = 演示模式。
        // 2 (QUNS_BUSY) 在普通大窗口下也会返回，误判会导致图标永远隐藏，故不采用。
        internal static bool ShellQuiet()
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) != 0) return false;
                return state == 3 || state == 4;
            }
            catch
            {
                return false;
            }
        }

        internal static bool ShouldHideForFullScreen()
        {
            try
            {
                if (ShellQuiet()) return true;

                // 覆盖整块屏幕、但**不是最大化**的窗口，才算真全屏（游戏/播放器/F11）
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;

                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                if (GetWindowPlacement(fg, ref placement) && placement.showCmd == 3) return false;

                return ForegroundCoversScreen();
            }
            catch
            {
                return false;
            }
        }

        internal static string FullScreenReport()
        {
            try
            {
                int state = -1;
                int hr = SHQueryUserNotificationState(out state);

                IntPtr fg = GetForegroundWindow();
                RECT win = new RECT();
                if (fg != IntPtr.Zero) GetWindowRect(fg, out win);

                int showCmd = -1;
                if (fg != IntPtr.Zero)
                {
                    WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                    placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    if (GetWindowPlacement(fg, ref placement)) showCmd = placement.showCmd;
                }

                IntPtr mon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
                MONITORINFO mi = new MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(mon, ref mi);

                return "hr=" + hr + " state=" + state + " showCmd=" + showCmd
                    + " win=" + win.Left + "," + win.Top + "," + win.Right + "," + win.Bottom
                    + " mon=" + mi.rcMonitor.Left + "," + mi.rcMonitor.Top + "," + mi.rcMonitor.Right + "," + mi.rcMonitor.Bottom
                    + " covers=" + ForegroundCoversScreen();
            }
            catch (Exception ex)
            {
                return "report failed: " + ex.Message;
            }
        }
    }
}
