using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClipPeek
{
    /// <summary>
    /// 开场动画：把图标切成细密的 12×12 小方块，从四周飞进图标应该在的位置拼起来。
    /// 方块只有 8px（96px 图标时），透明背景上像一小片像素尘埃汇聚成图标。
    /// 不依赖逐像素 alpha：整块整块地画，靠窗口的色键抠背景。
    /// </summary>
    internal sealed class IntroAnimation
    {
        internal sealed class Tile
        {
            internal Rectangle Source;
            internal Point Start;
            internal Point Target;
            internal int Delay;
            internal int Duration;
        }

        private const int Grid = 12;

        internal readonly int Box;
        internal readonly int Canvas;
        internal readonly int TotalMs;
        internal readonly int SettleMs;

        internal readonly List<Tile> Tiles = new List<Tile>();

        private readonly Bitmap _icon;
        private readonly Color _border;

        internal IntroAnimation(Bitmap icon, int box, int canvas, float scale, Random random)
        {
            _icon = icon;
            Box = box;
            Canvas = canvas;

            _border = Color.FromArgb(255, 152, 162, 178);

            int tileSize = Math.Max(2, box / Grid);
            int maxDelay = (int)Math.Round(260 * scale);
            int duration = (int)Math.Round(700 * scale);
            int settlePadding = (int)Math.Round(260 * scale);

            int center = canvas / 2;
            int originX = center - box / 2;
            int originY = center - box / 2;

            for (int row = 0; row < Grid; row++)
            {
                for (int col = 0; col < Grid; col++)
                {
                    Tile tile = new Tile();

                    int x = col * tileSize;
                    int y = row * tileSize;
                    int width = (col == Grid - 1) ? box - x : tileSize;
                    int height = (row == Grid - 1) ? box - y : tileSize;
                    if (width <= 0 || height <= 0) continue;

                    tile.Source = new Rectangle(x, y, width, height);
                    tile.Target = new Point(originX + x, originY + y);

                    double angle = random.NextDouble() * Math.PI * 2;
                    double distance = (95 + random.NextDouble() * 70) * scale;
                    tile.Start = new Point(
                        (int)Math.Round(tile.Target.X + Math.Cos(angle) * distance),
                        (int)Math.Round(tile.Target.Y + Math.Sin(angle) * distance));

                    tile.Delay = random.Next(maxDelay + 1);
                    tile.Duration = duration;

                    Tiles.Add(tile);
                }
            }

            SettleMs = maxDelay + duration;
            TotalMs = SettleMs + settlePadding;
        }

        private static double EaseOut(double t)
        {
            double inverse = 1.0 - t;
            return 1.0 - inverse * inverse * inverse;
        }

        internal void Draw(Graphics g, double elapsedMs)
        {
            // 整数像素、不做过任何抗锯齿：色键抠背景时边缘才不会出现粉色毛边
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.CompositingMode = CompositingMode.SourceCopy;

            using (Pen border = new Pen(_border, 1f))
            {
                foreach (Tile tile in Tiles)
                {
                    double local = (elapsedMs - tile.Delay) / tile.Duration;
                    if (local <= 0) continue;
                    if (local > 1) local = 1;

                    double eased = EaseOut(local);
                    int x = (int)Math.Round(tile.Start.X + (tile.Target.X - tile.Start.X) * eased);
                    int y = (int)Math.Round(tile.Start.Y + (tile.Target.Y - tile.Start.Y) * eased);

                    g.DrawImage(
                        _icon,
                        new Rectangle(x, y, tile.Source.Width, tile.Source.Height),
                        tile.Source.X, tile.Source.Y, tile.Source.Width, tile.Source.Height,
                        GraphicsUnit.Pixel);

                    // 还在飞的时候给个描边，白方块在浅色壁纸上才看得见；落位后收掉，拼出来是干净的图标
                    if (local < 1)
                    {
                        g.DrawRectangle(border, x, y, tile.Source.Width - 1, tile.Source.Height - 1);
                    }
                }
            }
        }
    }
}
