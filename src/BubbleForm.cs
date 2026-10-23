using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipPeek
{
    internal sealed class BubbleForm : Form
    {
        private const int LifeMs = 2500;

        internal event EventHandler Clicked;

        private readonly Timer _life;
        private readonly Timer _fade;
        private readonly Font _fontTitle;
        private readonly Font _fontLine;

        private readonly int _width;
        private readonly int _radius;
        private readonly int _pad;
        private readonly int _dot;
        private readonly int _titleX;
        private readonly int _titleY;
        private readonly int _titleH;
        private readonly int _lineY;
        private readonly int _lineStep;
        private readonly int _lineH;
        private readonly int _baseH;
        private readonly int _thumbMax;
        private readonly int _thumbH;

        private ClipItem _item;
        private string[] _lines;
        private bool _hovering;
        private bool _fadingIn;
        private int _thumbWidth;

        internal BubbleForm()
        {
            _width = Ui.Px(360);
            _radius = Ui.Px(12);
            _pad = Ui.Px(14);
            _dot = Ui.Px(18);
            _titleX = Ui.Px(40);
            _titleY = Ui.Px(11);
            _titleH = Ui.Px(20);
            _lineY = Ui.Px(33);
            _lineStep = Ui.Px(17);
            _lineH = Ui.Px(18);
            _baseH = Ui.Px(61);
            _thumbMax = Ui.Px(110);
            _thumbH = Ui.Px(74);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.BubbleBg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _fontTitle = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            _fontLine = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            _life = new Timer();
            _life.Interval = LifeMs;
            _life.Tick += OnLifeTick;

            _fade = new Timer();
            _fade.Interval = 16;
            _fade.Tick += OnFadeTick;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.MakeNoActivate(Handle);
        }

        internal void ShowItem(ClipItem item, Rectangle iconBounds)
        {
            _item = item;
            _lines = item == null ? new string[0] : item.BubbleLines;

            if (item != null && item.Preview != null)
            {
                _thumbWidth = Math.Min(_thumbMax, item.Preview.Width);
            }
            else
            {
                _thumbWidth = 0;
            }

            Layout_();
            Place(iconBounds);
            Present();
        }

        internal void ShowNote(string note, Rectangle iconBounds)
        {
            _item = null;
            _lines = new string[] { note == null ? string.Empty : note };
            _thumbWidth = 0;
            Layout_();
            Place(iconBounds);
            Present();
        }

        private void Layout_()
        {
            int lines = _lines == null ? 1 : Math.Max(1, _lines.Length);
            int height = _baseH + (lines - 1) * _lineStep;
            if (_thumbWidth > 0) height = Math.Max(height, _thumbH);
            Size = new Size(_width, height);
            Theme.RoundForm(this, _radius);
            Invalidate();
        }

        private void Place(Rectangle iconBounds)
        {
            int margin = Ui.Px(4);
            int gap = Ui.Px(10);

            Screen screen = Screen.FromRectangle(iconBounds);
            Rectangle work = screen.WorkingArea;

            int x = iconBounds.Left + iconBounds.Width / 2 - Width / 2;
            int y = iconBounds.Top - Height - gap;

            if (y < work.Top + margin) y = iconBounds.Bottom + gap;
            if (y + Height > work.Bottom) y = work.Bottom - Height - margin;

            x = Math.Max(work.Left + margin, Math.Min(x, work.Right - Width - margin));
            Location = new Point(x, y);
        }

        private void Present()
        {
            _hovering = false;
            Opacity = 0.0;
            if (!Visible) Show();
            TopMost = true;
            BringToFront();

            _fadingIn = true;
            _life.Stop();
            _fade.Stop();
            _fade.Start();
            _life.Start();
            Invalidate();
        }

        internal void HideBubble()
        {
            _life.Stop();
            _fade.Stop();
            _fadingIn = false;
            Hide();
        }

        private void OnLifeTick(object sender, EventArgs e)
        {
            _life.Stop();
            if (_hovering) return;
            _fadingIn = false;
            _fade.Start();
        }

        private void OnFadeTick(object sender, EventArgs e)
        {
            if (_fadingIn)
            {
                double up = Opacity + 0.20;
                if (up >= 1.0)
                {
                    Opacity = 1.0;
                    _fadingIn = false;
                    _fade.Stop();
                    return;
                }
                Opacity = up;
                return;
            }

            double next = Opacity - 0.14;
            if (next <= 0.04)
            {
                _fade.Stop();
                Hide();
                Opacity = 1.0;
                return;
            }
            Opacity = next;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovering = true;
            _life.Stop();
            _fade.Stop();
            Opacity = 1.0;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovering = false;
            _life.Stop();
            _life.Start();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            if (Clicked != null) Clicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Round(outer, _radius))
            {
                using (SolidBrush brush = new SolidBrush(Theme.BubbleBg)) g.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.CardBorder, 1f)) g.DrawPath(pen, path);
            }

            Rectangle dotRect = new Rectangle(_pad, Ui.Px(13), _dot, _dot);
            using (SolidBrush brush = new SolidBrush(Theme.Flash)) g.FillEllipse(brush, dotRect);
            using (Pen pen = new Pen(Color.White, Math.Max(1.6f, Ui.Px(1.7f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float left = dotRect.Left + dotRect.Width * 0.25f;
                float mid = dotRect.Left + dotRect.Width * 0.42f;
                float right = dotRect.Left + dotRect.Width * 0.74f;
                float top = dotRect.Top + dotRect.Height * 0.28f;
                float bottom = dotRect.Top + dotRect.Height * 0.72f;
                g.DrawLine(pen, left, (top + bottom) / 2, mid, bottom);
                g.DrawLine(pen, mid, bottom, right, top);
            }

            int textRight = Width - _pad - (_thumbWidth > 0 ? _thumbWidth + _pad : 0);

            string title = _item == null ? "提示" : "已复制 · " + _item.TitleText;

            TextRenderer.DrawText(
                g, title, _fontTitle,
                new Rectangle(_titleX, _titleY, textRight - _titleX, _titleH),
                Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            int y = _lineY;
            int lineWidth = textRight - _pad;
            if (_lines != null)
            {
                for (int i = 0; i < _lines.Length && i < 2; i++)
                {
                    TextRenderer.DrawText(
                        g, _lines[i], _fontLine,
                        new Rectangle(_pad, y, lineWidth, _lineH),
                        Theme.TextSub,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                            | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    y += _lineStep;
                }
            }

            if (_thumbWidth > 0 && _item != null && _item.Preview != null)
            {
                int inset = Ui.Px(14);
                Rectangle thumb = new Rectangle(
                    Width - inset - _thumbWidth, inset, _thumbWidth, Height - inset * 2);
                using (GraphicsPath path = Theme.Round(thumb, Ui.Px(6)))
                {
                    Region old = g.Clip;
                    g.SetClip(path);
                    g.DrawImage(_item.Preview, thumb);
                    g.Clip = old;
                    using (Pen pen = new Pen(Theme.CardBorder, 1f)) g.DrawPath(pen, path);
                }
            }
        }

        internal void DisposeFonts()
        {
            _fontTitle.Dispose();
            _fontLine.Dispose();
        }
    }
}
