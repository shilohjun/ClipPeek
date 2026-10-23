using System;
using System.Drawing;

namespace ClipPeek
{
    internal static class Ui
    {
        internal static float Scale = 1f;

        internal static void Init()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float dpi = g.DpiX;
                    if (dpi > 0) Scale = dpi / 96f;
                }
            }
            catch
            {
                Scale = 1f;
            }

            if (Scale < 1f) Scale = 1f;
            if (Scale > 4f) Scale = 4f;
        }

        internal static int Px(float value)
        {
            return (int)Math.Round(value * Scale);
        }

        internal static Point P(Point value)
        {
            return new Point(Px(value.X), Px(value.Y));
        }
    }
}
