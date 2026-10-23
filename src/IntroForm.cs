using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipPeek
{
    /// <summary>
    /// 开场动画窗口：整窗透明（色键抠底，不是卡片），细小的方块在桌面上汇聚成图标，
    /// 拼好之后整窗淡出，把位置交给真正的浮动图标。
    /// 用色键而不是逐像素 alpha：方块走整数坐标、不做抗锯齿，边缘不会出现彩边。
    /// </summary>
    internal sealed class IntroForm : Form
    {
        private const int FadeMs = 220;
        private static readonly Color Transparent = Color.FromArgb(255, 255, 0, 255);

        internal event EventHandler Finished;

        private readonly IntroAnimation _animation;
        private readonly Timer _timer;
        private readonly DateTime _started;

        private double _fadeStartedAt = -1;
        private bool _finished;

        internal IntroForm(Bitmap icon, Rectangle iconBounds)
        {
            int box = iconBounds.Width;
            int canvas = box * 3;

            _animation = new IntroAnimation(icon, box, canvas, Ui.Scale, new Random());

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Transparent;
            TransparencyKey = Transparent;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            Size = new Size(canvas, canvas);
            Location = new Point(
                iconBounds.Left + box / 2 - canvas / 2,
                iconBounds.Top + box / 2 - canvas / 2);

            _started = DateTime.UtcNow;
            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += OnTick;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.MakeNoActivate(Handle);
        }

        internal void Begin()
        {
            Show();
            TopMost = true;
            BringToFront();
            _timer.Start();
        }

        internal void Skip()
        {
            Finish();
        }

        private void OnTick(object sender, EventArgs e)
        {
            double elapsed = (DateTime.UtcNow - _started).TotalMilliseconds;

            if (elapsed >= _animation.TotalMs && _fadeStartedAt < 0) _fadeStartedAt = elapsed;

            if (_fadeStartedAt >= 0)
            {
                double progress = (elapsed - _fadeStartedAt) / FadeMs;
                double opacity = 1.0 - progress;
                if (opacity <= 0.03)
                {
                    Finish();
                    return;
                }
                Opacity = opacity;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            _animation.Draw(e.Graphics, (DateTime.UtcNow - _started).TotalMilliseconds);
        }

        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            _timer.Stop();
            Hide();

            if (Finished != null) Finished(this, EventArgs.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
