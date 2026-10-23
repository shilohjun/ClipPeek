using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ClipPeek
{
    /// <summary>
    /// 自定义图标的加载、裁剪与渲染。
    /// 用户选好一张图后，按他的缩放/平移状态裁成正方形，存为数据目录下的 custom-icon.png。
    /// </summary>
    internal static class IconImage
    {
        internal const string FileName = "custom-icon.png";
        internal const int SnapshotSize = 256;

        internal static string Path_(string dataDirectory)
        {
            return Path.Combine(dataDirectory, FileName);
        }

        internal static bool Exists(string dataDirectory)
        {
            try
            {
                return File.Exists(Path_(dataDirectory));
            }
            catch
            {
                return false;
            }
        }

        internal static Bitmap Load(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                using (Bitmap source = new Bitmap(file))
                {
                    return new Bitmap(source);
                }
            }
            catch (Exception ex)
            {
                Log.Error("IconImage.Load", ex);
                return null;
            }
        }

        internal static void Delete(string dataDirectory)
        {
            try
            {
                string file = Path_(dataDirectory);
                if (File.Exists(file)) File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Error("IconImage.Delete", ex);
            }
        }

        /// <summary>
        /// 把源图按 cover 方式铺满 dest：zoom = 1 时刚好铺满，center 是 0..1 的可见区域中心。
        /// </summary>
        internal static void DrawCover(Graphics g, Bitmap source, Rectangle dest, float zoom, PointF center)
        {
            if (source == null || dest.Width <= 0 || dest.Height <= 0) return;

            float baseScale = Math.Max((float)dest.Width / source.Width, (float)dest.Height / source.Height);
            float scale = baseScale * Math.Max(1f, zoom);

            float width = source.Width * scale;
            float height = source.Height * scale;
            float x = dest.Left + (dest.Width - width) * center.X;
            float y = dest.Top + (dest.Height - height) * center.Y;

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, x, y, width, height);
        }

        /// <summary>按圆角方形裁出一枚图标。</summary>
        internal static Bitmap RenderIcon(Bitmap source, int size, float zoom, PointF center, int radius)
        {
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Rectangle outer = new Rectangle(0, 0, size, size);
                using (GraphicsPath path = Theme.Round(outer, radius))
                {
                    g.SetClip(path);
                    DrawCover(g, source, outer, zoom, center);
                }
            }
            return bmp;
        }

        /// <summary>裁好后落盘（256×256 PNG）。</summary>
        internal static bool SaveSnapshot(Bitmap source, float zoom, PointF center, string dataDirectory)
        {
            try
            {
                int size = SnapshotSize;
                using (Bitmap snapshot = RenderIcon(source, size, zoom, center, Math.Max(8, size / 5)))
                {
                    Directory.CreateDirectory(dataDirectory);
                    snapshot.Save(Path_(dataDirectory), ImageFormat.Png);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("IconImage.SaveSnapshot", ex);
                return false;
            }
        }

        internal static bool LooksLikeImage(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".ico"
                || ext == ".gif" || ext == ".tif" || ext == ".tiff" || ext == ".webp";
        }
    }
}
