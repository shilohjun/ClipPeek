using System;
using System.Drawing;
using System.Drawing.Imaging;
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

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hDc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr hDc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UpdateLayeredWindow(
            IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE
        {
            public int CX;
            public int CY;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

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

        internal static int LastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }

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

        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int WS_EX_TRANSPARENT = 0x00000020;

        /// <summary>分层 + 鼠标穿透：动画窗口既按像素透明，又永远不挡点击。</summary>
        internal static void MakeLayeredClickThrough(IntPtr handle)
        {
            int ex = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE,
                ex | WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        internal static extern IntPtr CreateDIBSection(
            IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr bits, IntPtr hSection, uint offset);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        internal static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        /// <summary>
        /// 用 UpdateLayeredWindow 推一帧 32 位预乘 ARGB 位图。
        /// 必须自己建 DIB 段并拷贝像素——GetHbitmap 返回的是 DDB，ULW 只认 DIB 段。
        /// </summary>
        internal static bool SetLayeredBitmap(IntPtr handle, Bitmap bitmap, Point location)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr dib = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                BITMAPINFO info = new BITMAPINFO();
                info.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                info.bmiHeader.biWidth = bitmap.Width;
                info.bmiHeader.biHeight = bitmap.Height; // 自下而上
                info.bmiHeader.biPlanes = 1;
                info.bmiHeader.biBitCount = 32;
                info.bmiHeader.biCompression = 0;

                IntPtr bits;
                dib = CreateDIBSection(memoryDc, ref info, 0, out bits, IntPtr.Zero, 0);
                if (dib == IntPtr.Zero || bits == IntPtr.Zero) return false;

                BitmapData data = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppPArgb);
                try
                {
                    int rowBytes = bitmap.Width * 4;
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        // DIB 是自下而上，源位图是自上而下
                        long destinationOffset = (long)(bitmap.Height - 1 - y) * rowBytes;
                        long sourceOffset = (long)y * data.Stride;
                        CopyMemory(
                            new IntPtr(bits.ToInt64() + destinationOffset),
                            new IntPtr(data.Scan0.ToInt64() + sourceOffset),
                            rowBytes);
                    }
                    TraceFirstPixel("dib", Marshal.ReadInt32(bits));
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                oldBitmap = SelectObject(memoryDc, dib);

                POINT destination = new POINT();
                destination.X = location.X;
                destination.Y = location.Y;

                POINT source = new POINT();
                SIZE size = new SIZE();
                size.CX = bitmap.Width;
                size.CY = bitmap.Height;

                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1; // AC_SRC_ALPHA

                return UpdateLayeredWindow(handle, screenDc, ref destination, ref size,
                    memoryDc, ref source, 0, ref blend, 2);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memoryDc, oldBitmap);
                if (dib != IntPtr.Zero) DeleteObject(dib);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static bool _probed;

        internal static void TraceFirstPixel(string where, int pixel)
        {
            if (_probed) return;
            _probed = true;
            Program.Trace("pixel probe " + where + " = 0x" + pixel.ToString("X8"));
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
