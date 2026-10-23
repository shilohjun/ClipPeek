using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipPeek
{
    internal static class Theme
    {
        internal static bool Dark = true;

        internal static Color PanelBg;
        internal static Color CardBg;
        internal static Color CardBorder;
        internal static Color TextMain;
        internal static Color TextSub;
        internal static Color Accent;
        internal static Color RowHover;
        internal static Color RowSelected;
        internal static Color Separator;
        internal static Color BubbleBg;
        internal static Color Flash;

        internal static void Detect()
        {
            bool dark = true;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int) dark = ((int)value) == 0;
                    }
                }
            }
            catch
            {
            }

            Dark = dark;
            Flash = Color.FromArgb(255, 52, 211, 120);

            if (dark)
            {
                PanelBg = Color.FromArgb(255, 30, 34, 42);
                CardBg = Color.FromArgb(255, 38, 43, 53);
                CardBorder = Color.FromArgb(255, 70, 78, 92);
                TextMain = Color.FromArgb(255, 238, 241, 246);
                TextSub = Color.FromArgb(255, 141, 150, 165);
                Accent = Color.FromArgb(255, 64, 156, 255);
                RowHover = Color.FromArgb(255, 52, 58, 70);
                RowSelected = Color.FromArgb(255, 45, 70, 105);
                Separator = Color.FromArgb(255, 48, 54, 66);
                BubbleBg = Color.FromArgb(255, 24, 28, 36);
            }
            else
            {
                PanelBg = Color.FromArgb(255, 252, 252, 253);
                CardBg = Color.FromArgb(255, 245, 246, 249);
                CardBorder = Color.FromArgb(255, 213, 218, 226);
                TextMain = Color.FromArgb(255, 28, 32, 40);
                TextSub = Color.FromArgb(255, 112, 120, 134);
                Accent = Color.FromArgb(255, 0, 110, 220);
                RowHover = Color.FromArgb(255, 238, 242, 248);
                RowSelected = Color.FromArgb(255, 214, 231, 252);
                Separator = Color.FromArgb(255, 228, 232, 240);
                BubbleBg = Color.FromArgb(255, 255, 255, 255);
            }
        }

        internal static GraphicsPath Round(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void RoundForm(Form form, int radius)
        {
            using (GraphicsPath path = Round(new Rectangle(0, 0, form.Width, form.Height), radius))
            {
                Region old = form.Region;
                form.Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        /// <summary>用户自定义图标（256×256 方图）；为 null 时用内置绘制。</summary>
        internal static Bitmap CustomIcon;

        internal static Bitmap BuildBitmap(int size, bool flash)
        {
            if (CustomIcon != null) return BuildCustomBitmap(size, flash);

            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float s = size;
                Rectangle outer = new Rectangle(0, 0, size - 1, size - 1);
                Color bg = Dark ? Color.FromArgb(255, 24, 29, 38) : Color.FromArgb(255, 255, 255, 255);

                using (GraphicsPath path = Round(outer, Math.Max(8, (int)(s * 0.30f))))
                {
                    using (SolidBrush brush = new SolidBrush(bg)) g.FillPath(brush, path);

                    Color border = flash
                        ? Flash
                        : (Dark ? Color.FromArgb(255, 68, 78, 94) : Color.FromArgb(255, 203, 210, 222));
                    float width = flash ? Math.Max(2.0f, s / 16.0f) : Math.Max(1.0f, s / 48.0f);
                    using (Pen pen = new Pen(border, width)) g.DrawPath(pen, path);
                }

                Color glyph = flash
                    ? Flash
                    : (Dark ? Color.FromArgb(255, 232, 238, 248) : Color.FromArgb(255, 38, 44, 56));

                Rectangle board = new Rectangle(
                    (int)(s * 0.29f), (int)(s * 0.24f), (int)(s * 0.42f), (int)(s * 0.53f));
                using (GraphicsPath path = Round(board, (int)(s * 0.09f)))
                {
                    using (SolidBrush brush = new SolidBrush(glyph)) g.FillPath(brush, path);
                }

                // 板子上的三条横线，用背景色挖空
                using (Pen pen = new Pen(bg, Math.Max(1.0f, s / 30.0f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    float x1 = s * 0.38f, x2 = s * 0.62f;
                    g.DrawLine(pen, x1, s * 0.44f, x2, s * 0.44f);
                    g.DrawLine(pen, x1, s * 0.55f, x2, s * 0.55f);
                    g.DrawLine(pen, x1, s * 0.66f, x1 + (x2 - x1) * 0.55f, s * 0.66f);
                }

                Rectangle clip = new Rectangle(
                    (int)(s * 0.38f), (int)(s * 0.15f), (int)(s * 0.24f), (int)(s * 0.13f));
                using (GraphicsPath path = Round(clip, (int)(s * 0.05f)))
                {
                    using (SolidBrush brush = new SolidBrush(bg)) g.FillPath(brush, path);
                    using (Pen pen = new Pen(glyph, Math.Max(1.0f, s / 26.0f))) g.DrawPath(pen, path);
                }
            }
            return bmp;
        }

        private static Bitmap BuildCustomBitmap(int size, bool flash)
        {
            int radius = Math.Max(8, (int)(size * 0.30f));
            Bitmap bmp = IconImage.RenderIcon(CustomIcon, size, 1f, new PointF(0.5f, 0.5f), radius);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle outer = new Rectangle(0, 0, size - 1, size - 1);
                using (GraphicsPath path = Round(outer, radius))
                {
                    Color border = flash
                        ? Flash
                        : (Dark ? Color.FromArgb(255, 68, 78, 94) : Color.FromArgb(255, 203, 210, 222));
                    float width = flash ? Math.Max(2.0f, size / 16.0f) : Math.Max(1.0f, size / 48.0f);
                    using (Pen pen = new Pen(border, width)) g.DrawPath(pen, path);
                }
            }

            return bmp;
        }

        internal static void SetCustomIcon(Bitmap icon)
        {
            if (CustomIcon != null)
            {
                CustomIcon.Dispose();
                CustomIcon = null;
            }
            CustomIcon = icon;
        }

        internal static Icon MakeIcon(int size)
        {
            using (Bitmap bmp = BuildBitmap(size, false))
            {
                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(handle))
                    {
                        return (Icon)temp.Clone();
                    }
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }
    }
}
