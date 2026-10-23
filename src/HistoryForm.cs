using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipPeek
{
    internal sealed class HistoryForm : Form
    {
        internal event EventHandler<ClipItem> ItemActivated;
        internal event EventHandler HistoryChanged;
        internal event EventHandler<string> ImageDropped;
        internal event EventHandler PositionChanged;
        internal event EventHandler ShowIconRequested;

        /// <summary>浮动图标是否处于隐藏状态（面板页脚会给出「点这里显示」的入口）。</summary>
        internal bool IconHidden;

        private readonly HistoryStore _store;
        private readonly ListBox _list;
        private readonly Label _empty;

        private readonly Font _fontTitle;
        private readonly Font _fontItem;
        private readonly Font _fontMeta;

        private readonly int _w;
        private readonly int _headerH;
        private readonly int _footerH;
        private readonly int _rowH;
        private readonly int _radius;

        private Rectangle _clearRect;
        private Rectangle _closeRect;
        private bool _hotClear;
        private bool _hotClose;
        private int _hover = -1;
        private bool _menuOpen;
        private ContextMenuStrip _menu;

        private bool _dragging;
        private bool _dragMoved;
        private bool _suppressClick;
        private bool _hotFooter;
        private Point _dragFrom;
        private Point _windowFrom;

        private Rectangle FooterRect
        {
            get { return new Rectangle(0, Height - _footerH, _w, _footerH); }
        }

        internal int PanelWidth
        {
            get { return _w; }
        }

        internal HistoryForm(HistoryStore store)
        {
            _store = store;

            _w = Ui.Px(380);
            _headerH = Ui.Px(44);
            _footerH = Ui.Px(26);
            _rowH = Ui.Px(58);
            _radius = Ui.Px(14);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.PanelBg;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _fontTitle = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold, GraphicsUnit.Point);
            _fontItem = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _fontMeta = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point);

            _list = new ListBox();
            _list.BorderStyle = BorderStyle.None;
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.ItemHeight = _rowH;
            _list.IntegralHeight = false;
            _list.SelectionMode = SelectionMode.One;
            _list.BackColor = Theme.PanelBg;
            _list.ForeColor = Theme.TextMain;
            _list.DrawItem += OnDrawItem;
            _list.MouseMove += OnListMouseMove;
            _list.MouseLeave += OnListMouseLeave;
            _list.MouseClick += OnListMouseClick;
            _list.KeyDown += OnListKeyDown;

            _empty = new Label();
            _empty.Text = "还没有记录 · 复制点什么试试";
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Theme.TextSub;
            _empty.Font = _fontItem;
            _empty.BackColor = Theme.PanelBg;

            Controls.Add(_list);
            Controls.Add(_empty);

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
            Native.MakeToolWindow(Handle);
        }

        internal void ShowPanel(Rectangle iconBounds, bool useManualPosition, Point manualPosition)
        {
            Rebuild();

            Screen screen = Screen.FromRectangle(iconBounds);
            Rectangle work = screen.WorkingArea;

            int maxListHeight = Math.Max(Ui.Px(160), (int)(work.Height * 0.62));
            int listHeight = _store.Items.Count == 0 ? Ui.Px(96) : _store.Items.Count * _rowH;
            if (listHeight > maxListHeight) listHeight = maxListHeight;

            Size = new Size(_w, _headerH + _footerH + listHeight);
            Theme.RoundForm(this, _radius);

            _list.Bounds = new Rectangle(0, _headerH, _w, listHeight);
            _empty.Bounds = new Rectangle(0, _headerH, _w, listHeight);
            _empty.Visible = _store.Items.Count == 0;
            _list.Visible = !_empty.Visible;

            ComputeButtonRects();

            if (useManualPosition)
            {
                Location = ClampToScreen(manualPosition, screen);
            }
            else
            {
                Place(iconBounds);
            }

            Show();
            Activate();
            BringToFront();

            if (_store.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
                _list.Focus();
            }
            else
            {
                _empty.Focus();
            }

            Invalidate();
        }

        private Point ClampToScreen(Point location, Screen screen)
        {
            Rectangle work = screen.WorkingArea;
            int margin = Ui.Px(4);
            int x = Math.Max(work.Left + margin, Math.Min(location.X, work.Right - Width - margin));
            int y = Math.Max(work.Top + margin, Math.Min(location.Y, work.Bottom - Height - margin));
            return new Point(x, y);
        }

        internal void HidePanel()
        {
            _menuOpen = false;
            Hide();
        }

        private void Rebuild()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ClipItem item in _store.Items)
            {
                if (item.Kind == ClipKind.Files) item.FilesMissing = FilesGone(item);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _hover = -1;
        }

        private static bool FilesGone(ClipItem item)
        {
            if (item.Files == null || item.Files.Length == 0) return true;
            foreach (string path in item.Files)
            {
                try
                {
                    if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path)) return false;
                }
                catch
                {
                }
            }
            return true;
        }

        private void ComputeButtonRects()
        {
            _closeRect = new Rectangle(_w - Ui.Px(36), Ui.Px(10), Ui.Px(24), Ui.Px(24));
            _clearRect = new Rectangle(_w - Ui.Px(92), Ui.Px(12), Ui.Px(48), Ui.Px(20));
        }

        private void Place(Rectangle iconBounds)
        {
            int margin = Ui.Px(4);
            int gap = Ui.Px(12);

            Screen screen = Screen.FromRectangle(iconBounds);
            Rectangle work = screen.WorkingArea;

            int x = iconBounds.Left - Width - gap;
            if (x < work.Left + margin) x = iconBounds.Right + gap;
            x = Math.Max(work.Left + margin, Math.Min(x, work.Right - Width - margin));

            int y = iconBounds.Top + iconBounds.Height / 2 - Height / 2;
            y = Math.Max(work.Top + margin, Math.Min(y, work.Bottom - Height - margin));

            Location = new Point(x, y);
        }

        private void ApplyClipboard(ClipItem item)
        {
            if (item == null) return;
            if (ItemActivated != null) ItemActivated(this, item);
        }

        private void ActivateSelected()
        {
            if (_store.Items.Count == 0) return;
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _store.Items.Count) return;
            ApplyClipboard(_store.Items[index]);
        }

        private void DeleteSelected()
        {
            int index = _list.SelectedIndex;
            if (index < 0 || index >= _store.Items.Count) return;
            _store.RemoveAt(index);
            Rebuild();
            if (_store.Items.Count == 0) { _empty.Visible = true; _list.Visible = false; }
            if (HistoryChanged != null) HistoryChanged(this, EventArgs.Empty);
            Invalidate();
        }

        private void ClearAll()
        {
            _store.Clear();
            Rebuild();
            _empty.Visible = true;
            _list.Visible = false;
            if (HistoryChanged != null) HistoryChanged(this, EventArgs.Empty);
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { HidePanel(); return true; }
            if (keyData == Keys.Enter) { ActivateSelected(); return true; }
            if (keyData == Keys.Delete) { DeleteSelected(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { ActivateSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { HidePanel(); e.Handled = true; }
        }

        private void OnListMouseMove(object sender, MouseEventArgs e)
        {
            int index = _list.IndexFromPoint(e.Location);
            if (index != _hover)
            {
                _hover = index;
                _list.Invalidate();
            }
        }

        private void OnListMouseLeave(object sender, EventArgs e)
        {
            if (_hover != -1)
            {
                _hover = -1;
                _list.Invalidate();
            }
        }

        private void OnListMouseClick(object sender, MouseEventArgs e)
        {
            int index = _list.IndexFromPoint(e.Location);
            if (index < 0 || index >= _store.Items.Count) return;

            if (e.Button == MouseButtons.Right)
            {
                _list.SelectedIndex = index;
                ShowRowMenu(index);
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                _list.SelectedIndex = index;
                ApplyClipboard(_store.Items[index]);
            }
        }

        private void ShowRowMenu(int index)
        {
            if (_menu != null) { _menu.Dispose(); _menu = null; }

            _menu = new ContextMenuStrip();
            _menu.Items.Add("复制回剪贴板", null, delegate { ApplyClipboard(_store.Items[index]); });
            _menu.Items.Add("删除这条", null, delegate
            {
                _list.SelectedIndex = index;
                DeleteSelected();
            });
            _menu.Opened += delegate { _menuOpen = true; };
            _menu.Closed += delegate
            {
                _menuOpen = false;
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) HidePanel();
            };
            _menu.Show(_list, new Point(_list.Width / 2, index * _rowH - _list.Top + Ui.Px(20)));
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (_menuOpen) return;
            HidePanel();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (_closeRect.Contains(e.Location) || _clearRect.Contains(e.Location)) return;
            if (IconHidden && FooterRect.Contains(e.Location)) return;

            // 面板空白处（表头 / 页脚 / 列表外的边距）按住就能拖
            _dragging = true;
            _dragMoved = false;
            _dragFrom = Cursor.Position;
            _windowFrom = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragging)
            {
                Point now = Cursor.Position;
                int dx = now.X - _dragFrom.X;
                int dy = now.Y - _dragFrom.Y;
                if (!_dragMoved && (Math.Abs(dx) > Ui.Px(4) || Math.Abs(dy) > Ui.Px(4))) _dragMoved = true;
                if (_dragMoved) Location = new Point(_windowFrom.X + dx, _windowFrom.Y + dy);
                return;
            }

            bool hc = _clearRect.Contains(e.Location);
            bool hx = _closeRect.Contains(e.Location);
            bool hf = IconHidden && FooterRect.Contains(e.Location);
            if (hc != _hotClear || hx != _hotClose || hf != _hotFooter)
            {
                _hotClear = hc;
                _hotClose = hx;
                _hotFooter = hf;
                Invalidate(new Rectangle(0, 0, _w, _headerH));
                Invalidate(FooterRect);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !_dragging) return;

            bool moved = _dragMoved;
            _dragging = false;
            _dragMoved = false;

            if (moved)
            {
                _suppressClick = true;
                if (PositionChanged != null) PositionChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            if (_suppressClick)
            {
                _suppressClick = false;
                return;
            }

            if (e.Button != MouseButtons.Left) return;
            if (_closeRect.Contains(e.Location)) { HidePanel(); return; }
            if (_clearRect.Contains(e.Location)) { ClearAll(); return; }
            if (IconHidden && FooterRect.Contains(e.Location))
            {
                if (ShowIconRequested != null) ShowIconRequested(this, EventArgs.Empty);
                return;
            }
        }

        private void OnDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;

            ClipItem item = (ClipItem)_list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool hover = e.Index == _hover;

            Color background = selected ? Theme.RowSelected : (hover ? Theme.RowHover : Theme.PanelBg);
            using (SolidBrush brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);

            string[] lines = item.ContentLines;
            string first = lines.Length > 0 ? lines[0] : "(空)";

            TextRenderer.DrawText(
                e.Graphics, first, _fontItem,
                new Rectangle(e.Bounds.X + Ui.Px(12), e.Bounds.Y + Ui.Px(8),
                    e.Bounds.Width - Ui.Px(24), Ui.Px(20)),
                Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            string label = item.KindLabel;
            Size size = TextRenderer.MeasureText(label, _fontMeta);
            Rectangle pill = new Rectangle(
                e.Bounds.X + Ui.Px(12), e.Bounds.Y + Ui.Px(31),
                size.Width + Ui.Px(12), Ui.Px(18));

            using (GraphicsPath path = Theme.Round(pill, Ui.Px(9)))
            {
                using (SolidBrush brush = new SolidBrush(Theme.CardBg)) e.Graphics.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.CardBorder, 1f)) e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics, label, _fontMeta, pill, Theme.TextSub,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                e.Graphics, item.Subtitle, _fontMeta,
                new Rectangle(pill.Right + Ui.Px(8), pill.Top,
                    e.Bounds.Right - pill.Right - Ui.Px(20), Ui.Px(18)),
                Theme.TextSub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            if (!item.CanRestore)
            {
                TextRenderer.DrawText(
                    e.Graphics, "不可回填", _fontMeta,
                    new Rectangle(e.Bounds.Right - Ui.Px(76), pill.Top, Ui.Px(64), Ui.Px(18)),
                    Theme.TextSub,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else if (item.Kind == ClipKind.Files && item.FilesMissing)
            {
                TextRenderer.DrawText(
                    e.Graphics, "文件已不在", _fontMeta,
                    new Rectangle(e.Bounds.Right - Ui.Px(88), pill.Top, Ui.Px(76), Ui.Px(18)),
                    Theme.TextSub,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }

            using (Pen pen = new Pen(Theme.Separator, 1f))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left + Ui.Px(12), e.Bounds.Bottom - 1,
                    e.Bounds.Right - Ui.Px(12), e.Bounds.Bottom - 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle outer = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Round(outer, _radius))
            {
                using (SolidBrush brush = new SolidBrush(Theme.PanelBg)) g.FillPath(brush, path);
                using (Pen pen = new Pen(Theme.CardBorder, 1f)) g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                g, "剪贴历史", _fontTitle,
                new Rectangle(Ui.Px(14), Ui.Px(10), Ui.Px(130), Ui.Px(22)), Theme.TextMain,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(
                g, "共 " + _store.Items.Count + " 条", _fontMeta,
                new Rectangle(Ui.Px(120), Ui.Px(10), Ui.Px(140), Ui.Px(22)), Theme.TextSub,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            using (SolidBrush brush = new SolidBrush(_hotClear ? Theme.RowHover : Theme.PanelBg)) g.FillRectangle(brush, _clearRect);
            TextRenderer.DrawText(
                g, "清空", _fontMeta, _clearRect,
                _hotClear ? Theme.TextMain : Theme.TextSub,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            using (SolidBrush brush = new SolidBrush(_hotClose ? Theme.RowHover : Theme.PanelBg)) g.FillRectangle(brush, _closeRect);
            using (Pen pen = new Pen(_hotClose ? Theme.TextMain : Theme.TextSub, Math.Max(1.4f, Ui.Px(1.2f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                int cx = _closeRect.Left + _closeRect.Width / 2;
                int cy = _closeRect.Top + _closeRect.Height / 2;
                int arm = Ui.Px(5);
                g.DrawLine(pen, cx - arm, cy - arm, cx + arm, cy + arm);
                g.DrawLine(pen, cx + arm, cy - arm, cx - arm, cy + arm);
            }

            using (Pen pen = new Pen(Theme.Separator, 1f))
            {
                g.DrawLine(pen, Ui.Px(12), _headerH, Width - Ui.Px(12), _headerH);
                g.DrawLine(pen, Ui.Px(12), Height - _footerH, Width - Ui.Px(12), Height - _footerH);
            }

            if (IconHidden)
            {
                TextRenderer.DrawText(
                    g, "图标已隐藏 · 点这里显示浮动图标", _fontMeta,
                    new Rectangle(Ui.Px(14), Height - _footerH + Ui.Px(4), Width - Ui.Px(28), Ui.Px(18)),
                    _hotFooter ? Theme.TextMain : Theme.Accent,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(
                    g, "↑↓ 选择 · Enter 复制 · Del 删除 · Esc 关闭 · 可拖动", _fontMeta,
                    new Rectangle(Ui.Px(14), Height - _footerH + Ui.Px(4), Width - Ui.Px(28), Ui.Px(18)),
                    Theme.TextSub,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ComputeButtonRects();
        }

        internal void DisposeResources()
        {
            _fontTitle.Dispose();
            _fontItem.Dispose();
            _fontMeta.Dispose();
            if (_menu != null) _menu.Dispose();
        }
    }
}
