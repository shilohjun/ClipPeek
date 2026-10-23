using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipPeek
{
    internal sealed class IconForm : Form
    {
        internal event EventHandler Clicked;
        internal event EventHandler DragFinished;
        internal event EventHandler Pressed;
        internal event EventHandler<string> ImageDropped;

        internal ContextMenuStrip IconMenu;

        private readonly Timer _anim;
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
            MinimumSize = new Size(_box, _box);
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

            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
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
            if (Width != _box || Height != _box) Size = new Size(_box, _box);
            Theme.RoundForm(this, _radius);
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
            int margin = Ui.Px(8);
            Screen screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2));
            Rectangle work = screen.WorkingArea;
            int x = Math.Max(work.Left + margin, Math.Min(Left, work.Right - Width - margin));
            int y = Math.Max(work.Top + margin, Math.Min(Top, work.Bottom - Height - margin));
            Location = new Point(x, y);
        }

        internal void PlaceDefault()
        {
            int margin = Ui.Px(14);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - margin, work.Top + work.Height / 2 - Height / 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Bitmap source = _flashing ? _bmpFlash : _bmpNormal;
            if (source != null) e.Graphics.DrawImageUnscaled(source, 0, 0);
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
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _anim.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            bool busy = false;

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
