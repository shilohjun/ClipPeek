using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipPeek
{
    /// <summary>
    /// 复制成功时在鼠标位置闪一下的绿色圆环：跟着光标走、鼠标穿透、不抢焦点。
    /// 1.4 秒一次「呼吸 + 轻微扩张」，环的粗细固定，用窗口 Region 挖出中间的空洞。
    /// </summary>
    internal sealed class CursorGlow : Form
    {
        internal const int DurationMs = 1400;

        private readonly int _size;
        private readonly int _thickness;
        private readonly Timer _timer;

        private DateTime _started;
        private bool _active;

        private static readonly Color RingColor = Color.FromArgb(255, 46, 190, 118);

        internal CursorGlow()
        {
            _size = Ui.Px(72);
            _thickness = Math.Max(2, Ui.Px(3));

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(_size, _size);
            MinimumSize = new Size(_size, _size);
            MaximumSize = new Size(_size, _size);
            AutoScaleMode = AutoScaleMode.None;
            BackColor = RingColor;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += OnTick;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.MakeLayeredClickThrough(Handle);
        }

        internal bool IsPlaying
        {
            get { return _active; }
        }

        /// <summary>开始（或重新开始）一次呼吸。</summary>
        internal void Play()
        {
            _started = DateTime.UtcNow;
            _active = true;
            FollowCursor();

            if (!Visible) Show();
            ApplyShape(0);
            _timer.Start();
            Invalidate();
        }

        private void FollowCursor()
        {
            Point point = Cursor.Position;
            int half = _size / 2;
            Location = new Point(point.X - half, point.Y - half);
        }

        private double Progress
        {
            get
            {
                double elapsed = (DateTime.UtcNow - _started).TotalMilliseconds;
                double t = elapsed / DurationMs;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                return t;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!_active)
            {
                _timer.Stop();
                return;
            }

            double t = Progress;
            if (t >= 1)
            {
                _active = false;
                _timer.Stop();
                Hide();
                return;
            }

            FollowCursor();
            ApplyShape(t);
            Invalidate();
        }

        /// <summary>把窗口 Region 挖成一个圆环，中间那块完全透明、不挡鼠标也不糊字。</summary>
        private void ApplyShape(double t)
        {
            float outer = (float)(_size / 2.0 * 0.98 * RadiusScale(t));
            float inner = Math.Max(1f, outer - _thickness);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.FillMode = FillMode.Alternate;
                path.AddEllipse(_size / 2f - outer, _size / 2f - outer, outer * 2f, outer * 2f);
                path.AddEllipse(_size / 2f - inner, _size / 2f - inner, inner * 2f, inner * 2f);

                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }

            Opacity = OpacityAt(t);
        }

        /// <summary>半径随进度轻微扩张：0.62 → 1.0。</summary>
        internal static double RadiusScale(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            double eased = 1 - Math.Pow(1 - t, 2);
            return 0.62 + 0.38 * eased;
        }

        /// <summary>先快速亮起（前 10%），再慢慢淡掉。</summary>
        internal static double OpacityAt(double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            if (t < 0.10) return t / 0.10;

            double k = (t - 0.10) / 0.90;
            return 1.0 - k * k;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float outer = (float)(_size / 2.0 * 0.98 * RadiusScale(Progress));
            float inner = Math.Max(1f, outer - _thickness);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.FillMode = FillMode.Alternate;
                path.AddEllipse(_size / 2f - outer, _size / 2f - outer, outer * 2f, outer * 2f);
                path.AddEllipse(_size / 2f - inner, _size / 2f - inner, inner * 2f, inner * 2f);

                using (SolidBrush brush = new SolidBrush(RingColor))
                {
                    e.Graphics.FillPath(brush, path);
                }
            }
        }

        internal void Stop()
        {
            _active = false;
            _timer.Stop();
            Hide();
        }
    }
}
