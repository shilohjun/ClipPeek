using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ClipPeek
{
    /// <summary>
    /// 换图标时的裁剪对话框：拖动平移、滚轮缩放，右侧实时显示实际大小。
    /// </summary>
    internal sealed class IconCropForm : Form
    {
        private readonly Bitmap _source;
        private readonly string _dataDirectory;

        private readonly int _pad;
        private readonly int _previewSize;
        private readonly int _sideSize;
        private readonly int _radius;
        private readonly int _canvasWidth;
        private readonly int _canvasHeight;

        private readonly Rectangle _previewRect;
        private readonly Button _okButton;
        private readonly Button _cancelButton;
        private readonly Button _resetButton;

        private float _zoom = 1f;
        private PointF _center = new PointF(0.5f, 0.5f);
        private bool _dragging;
        private Point _dragFrom;
        private PointF _dragCenter;

        internal bool ResetRequested;

        internal IconCropForm(string imagePath, string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            _source = IconImage.Load(imagePath);
            if (_source == null) throw new InvalidOperationException("无法读取图片");

            Text = "更换图标";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Theme.PanelBg;
            ForeColor = Theme.TextMain;
            Font = new Font("Microsoft YaHei UI", 9f);

            _pad = Ui.Px(20);
            _previewSize = Ui.Px(260);
            _sideSize = Ui.Px(150);
            _radius = Ui.Px(14);
            _canvasWidth = _pad * 3 + _previewSize + _sideSize;
            _canvasHeight = _pad * 2 + _previewSize + Ui.Px(52);
            _previewRect = new Rectangle(_pad, _pad + Ui.Px(24), _previewSize, _previewSize);

            ClientSize = new Size(_canvasWidth, _canvasHeight + Ui.Px(44));

            // 手工居中到鼠标所在的屏幕：StartPosition.CenterScreen 在混合 DPI 多屏下会跑到屏幕外
            Screen screen = Screen.FromPoint(Cursor.Position);
            Rectangle work = screen.WorkingArea;
            Location = new Point(
                work.Left + Math.Max(0, (work.Width - Width) / 2),
                work.Top + Math.Max(0, (work.Height - Height) / 2));

            int buttonWidth = Ui.Px(88);
            int buttonHeight = Ui.Px(30);

            _okButton = new Button();
            _okButton.Text = "确定";
            _okButton.Size = new Size(buttonWidth, buttonHeight);
            _okButton.Location = new Point(ClientSize.Width - _pad - buttonWidth, ClientSize.Height - _pad - buttonHeight + Ui.Px(4));
            _okButton.Click += OnOk;

            _cancelButton = new Button();
            _cancelButton.Text = "取消";
            _cancelButton.Size = new Size(buttonWidth, buttonHeight);
            _cancelButton.Location = new Point(_okButton.Left - Ui.Px(8) - buttonWidth, _okButton.Top);
            _cancelButton.DialogResult = DialogResult.Cancel;

            _resetButton = new Button();
            _resetButton.Text = "恢复默认图标";
            _resetButton.Size = new Size(Ui.Px(120), buttonHeight);
            _resetButton.Location = new Point(_pad, _okButton.Top);
            _resetButton.Click += OnReset;

            Controls.Add(_okButton);
            Controls.Add(_cancelButton);
            Controls.Add(_resetButton);

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush brush = new SolidBrush(Theme.TextSub))
            {
                g.DrawString("拖动图片调整位置，滚轮缩放", Font, brush, _pad, _pad + Ui.Px(2));
            }

            // 预览框
            using (GraphicsPath path = Theme.Round(_previewRect, _radius))
            {
                using (SolidBrush brush = new SolidBrush(Theme.CardBg)) g.FillPath(brush, path);
                Region old = g.Clip;
                g.SetClip(path);
                IconImage.DrawCover(g, _source, _previewRect, _zoom, _center);
                g.Clip = old;
                using (Pen pen = new Pen(Theme.CardBorder, 1.4f)) g.DrawPath(pen, path);
            }

            // 右侧实际大小
            int sideX = _previewRect.Right + _pad;
            int sideY = _previewRect.Top;
            Rectangle large = new Rectangle(sideX, sideY + Ui.Px(22), Ui.Px(96), Ui.Px(96));
            Rectangle small = new Rectangle(sideX, sideY + Ui.Px(150), Ui.Px(48), Ui.Px(48));

            using (Bitmap rendered = IconImage.RenderIcon(_source, Ui.Px(96), _zoom, _center, Math.Max(8, Ui.Px(96) / 4)))
            {
                g.DrawImageUnscaled(rendered, large);
            }
            using (Bitmap rendered = IconImage.RenderIcon(_source, Ui.Px(48), _zoom, _center, Math.Max(6, Ui.Px(48) / 4)))
            {
                g.DrawImageUnscaled(rendered, small);
            }

            using (SolidBrush brush = new SolidBrush(Theme.TextSub))
            {
                g.DrawString("实际大小", Font, brush, sideX, sideY);
                g.DrawString("桌面上的图标", Font, brush, small.Right + Ui.Px(10), small.Top + Ui.Px(14));
                g.DrawString(Math.Round(_zoom * 100) + "%", Font, brush, sideX, small.Bottom + Ui.Px(12));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_previewRect.Contains(e.Location)) return;
            _dragging = true;
            _dragFrom = e.Location;
            _dragCenter = _center;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;

            float scaledWidth = GetScaledWidth();
            float scaledHeight = GetScaledHeight();

            float dx = e.X - _dragFrom.X;
            float dy = e.Y - _dragFrom.Y;

            if (scaledWidth > _previewRect.Width)
            {
                _center.X = Clamp(_dragCenter.X - dx / (scaledWidth - _previewRect.Width), 0f, 1f);
            }
            if (scaledHeight > _previewRect.Height)
            {
                _center.Y = Clamp(_dragCenter.Y - dy / (scaledHeight - _previewRect.Height), 0f, 1f);
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!_previewRect.Contains(e.Location)) return;

            float previous = _zoom;
            _zoom = Clamp(_zoom * (e.Delta > 0 ? 1.12f : 0.89f), 1f, 4f);
            if (_zoom <= 1.001f)
            {
                _zoom = 1f;
                _center = new PointF(0.5f, 0.5f);
            }
            else if (Math.Abs(previous - _zoom) > 0.001f)
            {
                _center = new PointF(Clamp(_center.X, 0f, 1f), Clamp(_center.Y, 0f, 1f));
            }
            Invalidate();
        }

        private float GetScaledWidth()
        {
            float baseScale = Math.Max((float)_previewRect.Width / _source.Width, (float)_previewRect.Height / _source.Height);
            return _source.Width * baseScale * _zoom;
        }

        private float GetScaledHeight()
        {
            float baseScale = Math.Max((float)_previewRect.Width / _source.Width, (float)_previewRect.Height / _source.Height);
            return _source.Height * baseScale * _zoom;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (!IconImage.SaveSnapshot(_source, _zoom, _center, _dataDirectory))
            {
                MessageBox.Show("保存图标失败，请检查数据目录权限。", "ClipPeek", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnReset(object sender, EventArgs e)
        {
            ResetRequested = true;
            IconImage.Delete(_dataDirectory);
            Theme.SetCustomIcon(null);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_source != null) _source.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
