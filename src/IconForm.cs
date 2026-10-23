using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipPeek
{
    internal enum SnapEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 3,
        Bottom = 4
    }

    /// <summary>吸附后的呼吸灯：很慢的明暗循环（一轮 4.2 秒）。</summary>
    internal static class DockBreath
    {
        internal const double PeriodMs = 4200;
        internal const double MinOpacity = 0.42;
        internal const double MaxOpacity = 1.0;

        internal static double OpacityAt(double elapsedMs)
        {
            double phase = (elapsedMs % PeriodMs) / PeriodMs;
            double wave = 0.5 - 0.5 * Math.Cos(phase * Math.PI * 2);
            return MinOpacity + (MaxOpacity - MinOpacity) * wave;
        }
    }

    internal sealed class IconForm : Form
    {
        internal event EventHandler Clicked;
        internal event EventHandler DragFinished;
        internal event EventHandler Pressed;
        internal event EventHandler<string> ImageDropped;

        internal ContextMenuStrip IconMenu;

        private readonly Timer _anim;
        private readonly Timer _dockTimer;
        private readonly int _box;
        private readonly int _radius;
        private readonly int _dragThreshold;

        private Bitmap _bmpNormal;
        private Bitmap _bmpFlash;

        private Point _dragOriginScreen;
        private Point _windowOrigin;
        private bool _mouseDown;
        private bool _dragging;
        private bool _hover;
        private bool _flashing;
        private DateTime _flashUntil;

        private SnapEdge _edge = SnapEdge.None;
        private bool _dockExpanded;
        private DateTime _dockStarted;
        private int _dockAlong;

        internal IconForm()
        {
            _box = Ui.Px(48);
            _radius = Ui.Px(14);
            _dragThreshold = Ui.Px(4);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(_box, _box);
            MinimumSize = new Size(Ui.Px(20), Ui.Px(20));
            MaximumSize = new Size(_box, _box);
            Opacity = 0.85;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.PanelBg;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _anim = new Timer();
            _anim.Interval = 30;
            _anim.Tick += OnAnimTick;

            _dockTimer = new Timer();
            _dockTimer.Interval = 40;
            _dockTimer.Tick += OnDockTick;

            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
        }

        internal bool IsDocked
        {
            get { return _edge != SnapEdge.None; }
        }

        internal SnapEdge Edge
        {
            get { return _edge; }
        }

        /// <summary>吸附后窗口只占图标的一半，露出贴着屏幕的那一半。</summary>
        private int DockWidth
        {
            get
            {
                if (_edge == SnapEdge.Left || _edge == SnapEdge.Right) return _box / 2;
                return _box;
            }
        }

        private int DockHeight
        {
            get
            {
                if (_edge == SnapEdge.Top || _edge == SnapEdge.Bottom) return _box / 2;
                return _box;
            }
        }

        /// <summary>画图标时把被屏幕边缘挡掉的那一半挪出窗口。</summary>
        private Point BitmapOffset
        {
            get
            {
                switch (_edge)
                {
                    case SnapEdge.Left: return new Point(-_box / 2, 0);
                    case SnapEdge.Top: return new Point(0, -_box / 2);
                    default: return new Point(0, 0);
                }
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = GetDroppedImage(e) == null ? DragDropEffects.None : DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string file = GetDroppedImage(e);
            if (file == null) return;
            if (ImageDropped != null) ImageDropped(this, file);
        }

        private static string GetDroppedImage(DragEventArgs e)
        {
            try
            {
                if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return null;
                return IconImage.LooksLikeImage(files[0]) ? files[0] : null;
            }
            catch
            {
                return null;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.MakeNoActivate(Handle);

            // 已经吸附时不能再把尺寸强行改回整枚图标，否则吸附状态会被立刻抹掉
            if (IsDocked)
            {
                ApplyDockGeometry();
            }
            else
            {
                if (Width != _box || Height != _box) Size = new Size(_box, _box);
                Theme.RoundForm(this, _radius);
            }

            RebuildBitmaps();
        }

        internal void RebuildBitmaps()
        {
            if (_bmpNormal != null) _bmpNormal.Dispose();
            if (_bmpFlash != null) _bmpFlash.Dispose();
            _bmpNormal = Theme.BuildBitmap(_box, false);
            _bmpFlash = Theme.BuildBitmap(_box, true);
            BackColor = Theme.PanelBg;
            Invalidate();
        }

        internal void Flash()
        {
            _flashing = true;
            _flashUntil = DateTime.UtcNow.AddMilliseconds(520);
            _anim.Start();
            Invalidate();
        }

        internal void ClampToScreen()
        {
            if (IsDocked)
            {
                ApplyDockGeometry();
                return;
            }

            int margin = Ui.Px(8);
            Screen screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Rectangle work = screen.WorkingArea;
            int x = Math.Max(work.Left + margin, Math.Min(Left, work.Right - Width - margin));
            int y = Math.Max(work.Top + margin, Math.Min(Top, work.Bottom - Height - margin));
            Location = new Point(x, y);
        }

        internal void PlaceDefault()
        {
            Undock();
            int margin = Ui.Px(14);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - margin, work.Top + work.Height / 2 - Height / 2);
        }

        // ---------- 吸附 ----------

        internal void DockTo(SnapEdge edge)
        {
            if (edge == SnapEdge.None) return;

            _edge = edge;
            _dockExpanded = false;
            _dockStarted = DateTime.UtcNow;
            _dockAlong = (edge == SnapEdge.Left || edge == SnapEdge.Right)
                ? Top + Height / 2
                : Left + Width / 2;

            ApplyDockGeometry();
            _dockTimer.Start();
            Invalidate();
        }

        internal void Undock()
        {
            bool wasDocked = IsDocked;
            _edge = SnapEdge.None;
            _dockExpanded = false;
            if (wasDocked)
            {
                _dockTimer.Stop();
                Size = new Size(_box, _box);
                Theme.RoundForm(this, _radius);
                Opacity = 0.85;

                // 从右边/下边解除吸附后窗口会多出半枚图标，必须拉回屏幕内
                ClampToScreen();
                Invalidate();
            }
        }

        private void ApplyDockGeometry()
        {
            if (!IsDocked) return;

            int width = _dockExpanded ? _box : DockWidth;
            int height = _dockExpanded ? _box : DockHeight;

            Screen screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Rectangle work = screen.WorkingArea;

            Size = new Size(width, height);

            int x = Left;
            int y = Top;

            switch (_edge)
            {
                case SnapEdge.Left:
                    x = work.Left;
                    y = _dockAlong - height / 2;
                    break;
                case SnapEdge.Right:
                    x = work.Right - width;
                    y = _dockAlong - height / 2;
                    break;
                case SnapEdge.Top:
                    x = _dockAlong - width / 2;
                    y = work.Top;
                    break;
                case SnapEdge.Bottom:
                    x = _dockAlong - width / 2;
                    y = work.Bottom - height;
                    break;
            }

            int margin = Ui.Px(4);
            if (_edge == SnapEdge.Left || _edge == SnapEdge.Right)
            {
                y = Math.Max(work.Top + margin, Math.Min(y, work.Bottom - height - margin));
                _dockAlong = y + height / 2;
            }
            else
            {
                x = Math.Max(work.Left + margin, Math.Min(x, work.Right - width - margin));
                _dockAlong = x + width / 2;
            }

            Location = new Point(x, y);
            ApplyDockRegion();
        }

        /// <summary>Region 就是整枚图标的圆角方形，超出窗口的那一半被系统裁掉，看起来就是「半个剪贴板」。</summary>
        private void ApplyDockRegion()
        {
            Point offset = _dockExpanded ? new Point(0, 0) : BitmapOffset;
            Rectangle shape = new Rectangle(offset.X, offset.Y, _box, _box);

            using (GraphicsPath path = Theme.Round(shape, _radius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private double DockElapsed
        {
            get { return (DateTime.UtcNow - _dockStarted).TotalMilliseconds; }
        }

        private void OnDockTick(object sender, EventArgs e)
        {
            if (!IsDocked)
            {
                _dockTimer.Stop();
                return;
            }

            if (_dockExpanded || _mouseDown || _hover)
            {
                if (Math.Abs(Opacity - 1.0) > 0.01) Opacity = 1.0;
                return;
            }

            // 呼吸灯：慢慢亮、慢慢暗
            Opacity = DockBreath.OpacityAt(DockElapsed);
        }

        // ---------- 交互 ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Bitmap source = _flashing ? _bmpFlash : _bmpNormal;
            if (source == null) return;

            Point offset = (IsDocked && !_dockExpanded) ? BitmapOffset : new Point(0, 0);
            e.Graphics.DrawImageUnscaled(source, offset);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Right)
            {
                if (IconMenu != null) IconMenu.Show(Cursor.Position);
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                _mouseDown = true;
                _dragging = false;
                _dragOriginScreen = Cursor.Position;
                _windowOrigin = Location;
                if (Pressed != null) Pressed(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_mouseDown) return;

            Point now = Cursor.Position;
            int dx = now.X - _dragOriginScreen.X;
            int dy = now.Y - _dragOriginScreen.Y;

            if (!_dragging && (Math.Abs(dx) > _dragThreshold || Math.Abs(dy) > _dragThreshold)) _dragging = true;
            if (!_dragging) return;

            // 从吸附状态拖出来：先解除吸附，再把锚点重置到这个位置，避免图标跳一下
            if (IsDocked)
            {
                _edge = SnapEdge.None;
                _dockTimer.Stop();
                _dockExpanded = false;
                Size = new Size(_box, _box);
                Theme.RoundForm(this, _radius);
                Location = new Point(
                    _dragOriginScreen.X - _box / 2,
                    _dragOriginScreen.Y - _box / 2);
                _dragOriginScreen = now;
                _windowOrigin = Location;
                dx = 0;
                dy = 0;
            }

            Opacity = 0.75;
            Location = new Point(_windowOrigin.X + dx, _windowOrigin.Y + dy);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;

            bool wasDragging = _dragging;
            _mouseDown = false;
            _dragging = false;
            _anim.Start();

            if (wasDragging)
            {
                ClampToScreen();
                if (DragFinished != null) DragFinished(this, EventArgs.Empty);
            }
            else
            {
                if (Clicked != null) Clicked(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            _anim.Start();

            // 吸附时鼠标移上去展开成完整图标
            if (IsDocked && !_dockExpanded)
            {
                _dockExpanded = true;
                ApplyDockGeometry();
                Opacity = 1.0;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _anim.Start();

            if (IsDocked && _dockExpanded && !_mouseDown)
            {
                Point cursor = Cursor.Position;
                if (CollapsedBounds().Contains(cursor)) return;

                _dockExpanded = false;
                _dockStarted = DateTime.UtcNow;
                ApplyDockGeometry();
                _dockTimer.Start();
                Invalidate();
            }
        }

        private Rectangle CollapsedBounds()
        {
            Screen screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Rectangle work = screen.WorkingArea;
            int width = DockWidth;
            int height = DockHeight;

            switch (_edge)
            {
                case SnapEdge.Left: return new Rectangle(work.Left, _dockAlong - height / 2, width, height);
                case SnapEdge.Right: return new Rectangle(work.Right - width, _dockAlong - height / 2, width, height);
                case SnapEdge.Top: return new Rectangle(_dockAlong - width / 2, work.Top, width, height);
                case SnapEdge.Bottom: return new Rectangle(_dockAlong - width / 2, work.Bottom - height, width, height);
                default: return Bounds;
            }
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            bool busy = false;

            if (IsDocked)
            {
                // 吸附时的不透明度由呼吸灯接管
                _anim.Stop();
                return;
            }

            float target = (_hover || _mouseDown || _dragging) ? 1.0f : (_flashing ? 0.98f : 0.85f);
            if (_dragging) target = 0.75f;

            double current = Opacity;
            if (Math.Abs(current - target) > 0.02)
            {
                double step = target > current ? 0.09 : -0.09;
                current += step;
                if (step > 0 && current > target) current = target;
                if (step < 0 && current < target) current = target;
                Opacity = current;
                busy = true;
            }

            if (_flashing)
            {
                if (DateTime.UtcNow > _flashUntil)
                {
                    _flashing = false;
                    Invalidate();
                }
                else
                {
                    busy = true;
                }
            }

            if (!busy) _anim.Stop();
        }

        internal void DisposeBitmaps()
        {
            if (_bmpNormal != null) { _bmpNormal.Dispose(); _bmpNormal = null; }
            if (_bmpFlash != null) { _bmpFlash.Dispose(); _bmpFlash = null; }
        }
    }
}
