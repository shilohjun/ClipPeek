using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ClipPeek
{
    internal sealed class ClipPeekContext : ApplicationContext
    {
        private sealed class HotKeyWindow : NativeWindow
        {
            internal event EventHandler Pressed;

            internal HotKeyWindow()
            {
                CreateParams cp = new CreateParams();
                cp.Caption = "ClipPeekHotKey";
                cp.X = 0;
                cp.Y = 0;
                cp.Width = 0;
                cp.Height = 0;
                cp.Style = 0;
                cp.ExStyle = Native.WS_EX_TOOLWINDOW;
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Native.WM_HOTKEY)
                {
                    if (Pressed != null) Pressed(this, EventArgs.Empty);
                    return;
                }
                base.WndProc(ref m);
            }
        }

        private const int HotKeyId = 0xB001;
        private const int MaxBubbleChars = 200000;

        private readonly string _directory;

        private Settings _settings;
        private HistoryStore _history;
        private IconForm _icon;
        private BubbleForm _bubble;
        private HistoryForm _panel;
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _pauseItem;
        private ToolStripMenuItem _autoItem;
        private ToolStripMenuItem _hideItem;
        private ToolStripMenuItem _clearItem;

        private System.Windows.Forms.Timer _clipTimer;
        private System.Windows.Forms.Timer _fullScreenTimer;
        private HotKeyWindow _hotKeyWindow;
        private bool _hotKeyRegistered;

        private uint _lastSeq;
        private DateTime _suppressUntil = DateTime.MinValue;
        private bool _hiddenForFullScreen;
        private bool _sawPanelVisible;
        private EventWaitHandle _quitEvent;
        private RegisteredWaitHandle _quitWait;

        private Rectangle _anchorOverride;
        private bool _hasAnchorOverride;

        private bool _started;

        internal ClipPeekContext(string directory)
        {
            _directory = directory;
            Log.Init(directory);
            Theme.Detect();
            Initialize();
        }

        private Rectangle Anchor
        {
            get
            {
                if (_hasAnchorOverride) return _anchorOverride;
                return _icon.Bounds;
            }
        }

        private void Initialize()
        {
            _settings = new Settings(Path.Combine(_directory, "settings.json"));
            _history = new HistoryStore(Path.Combine(_directory, "history.json"));

            _icon = new IconForm();
            _icon.Pressed += OnIconPressed;
            _icon.Clicked += OnIconClicked;
            _icon.DragFinished += OnIconDragFinished;

            _bubble = new BubbleForm();
            _bubble.Clicked += OnBubbleClicked;

            _panel = new HistoryForm(_history);
            _panel.ItemActivated += OnItemActivated;

            BuildMenu();
            _icon.IconMenu = _menu;

            PositionIcon();
        }

        internal void Start()
        {
            if (_started) return;
            _started = true;

            _icon.Show();
            ApplyIconVisibility();
            _icon.RebuildBitmaps();
            Program.Trace("start: icon shown at " + _icon.Left + "," + _icon.Top);

            CreateTray();
            Program.Trace("start: tray ok");

            RegisterHotKey();
            Program.Trace("start: hotkey=" + _hotKeyRegistered);

            StartTimers();
            EnsureAutostart();
            StartQuitWatcher();

            _lastSeq = Native.GetClipboardSequenceNumber();
            Log.Info("started");
            Program.Trace("start: ready autostart=" + Autostart.IsEnabled() + " theme=" + (Theme.Dark ? "dark" : "light"));
            if (Environment.GetEnvironmentVariable("CLIPPEEK_TRACE") != null)
            {
                foreach (Screen s in Screen.AllScreens)
                {
                    Program.Trace("screen " + s.DeviceName + " primary=" + s.Primary
                        + " bounds=" + s.Bounds + " work=" + s.WorkingArea);
                }
                Program.Trace("icon final bounds=" + _icon.Bounds);
                Program.Trace("fullscreen-hide=" + Native.ShouldHideForFullScreen());
                Program.Trace("fullscreen-report: " + Native.FullScreenReport());
            }
        }

        private void PositionIcon()
        {
            if (_settings.HasPosition)
            {
                _icon.Location = new Point(_settings.IconX, _settings.IconY);
                _icon.ClampToScreen();
            }
            else
            {
                _icon.PlaceDefault();
            }
        }

        private void CreateTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = Theme.MakeIcon(Ui.Px(32));
            _tray.Text = "ClipPeek · 剪贴板气泡与历史";
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.MouseClick += OnTrayClick;
            _tray.DoubleClick += OnTrayDoubleClick;
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) TogglePanel();
        }

        private void OnTrayDoubleClick(object sender, EventArgs e)
        {
            _settings.Hidden = false;
            _settings.Save();
            ApplyIconVisibility();
        }

        private void RegisterHotKey()
        {
            try
            {
                _hotKeyWindow = new HotKeyWindow();
                _hotKeyWindow.Pressed += OnHotKey;
                _hotKeyRegistered = Native.RegisterHotKey(
                    _hotKeyWindow.Handle, HotKeyId,
                    Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, 0x56);
                if (!_hotKeyRegistered)
                {
                    Log.Error("RegisterHotKey Ctrl+Alt+V failed", null);
                }
            }
            catch (Exception ex)
            {
                Log.Error("RegisterHotKey", ex);
            }
        }

        private void StartTimers()
        {
            _clipTimer = new System.Windows.Forms.Timer();
            _clipTimer.Interval = 250;
            _clipTimer.Tick += OnClipTimer;
            _clipTimer.Start();

            _fullScreenTimer = new System.Windows.Forms.Timer();
            _fullScreenTimer.Interval = 1000;
            _fullScreenTimer.Tick += OnFullScreenTimer;
            _fullScreenTimer.Start();
        }

        private void StartQuitWatcher()
        {
            bool created;
            _quitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "ClipPeek.Quit", out created);
            _quitEvent.Reset();
            _quitWait = ThreadPool.RegisterWaitForSingleObject(
                _quitEvent, OnQuitSignalled, null, Timeout.Infinite, true);
        }

        private void OnQuitSignalled(object state, bool timedOut)
        {
            try
            {
                if (_icon.IsHandleCreated) _icon.BeginInvoke((MethodInvoker)ExitApp);
            }
            catch
            {
            }
        }

        private void OnClipTimer(object sender, EventArgs e)
        {
            if (_settings.Paused) return;

            uint seq = Native.GetClipboardSequenceNumber();
            if (seq == _lastSeq) return;
            _lastSeq = seq;

            if (DateTime.UtcNow < _suppressUntil) return;

            ProcessClipboard();
        }

        private bool ProcessClipboard()
        {
            string error;
            ClipItem item = ClipboardWatcher.Capture(out error);
            if (error != null) Log.Error("ClipboardWatcher.Capture", new Exception(error));
            if (item == null) return false;

            _history.Add(item);
            _icon.Flash();
            Program.Trace("clip captured: kind=" + item.Kind + " chars=" + item.Chars + " len=" + item.Text);

            if (item.Kind == ClipKind.Text && item.Chars > MaxBubbleChars)
            {
                Log.Info("skip bubble, text too long: " + item.Chars);
                return true;
            }

            _bubble.ShowItem(item, Anchor);
            return true;
        }

        private void OnFullScreenTimer(object sender, EventArgs e)
        {
            bool hide;
            try
            {
                hide = Native.ShouldHideForFullScreen();
            }
            catch (Exception ex)
            {
                Log.Error("ShouldHideForFullScreen", ex);
                hide = false;
            }

            if (hide == _hiddenForFullScreen) return;

            _hiddenForFullScreen = hide;
            Program.Trace("fs timer: hide=" + hide);
            ApplyIconVisibility();
        }

        private void ApplyIconVisibility()
        {
            bool show = !_settings.Hidden && !_hiddenForFullScreen;
            Program.Trace("apply visibility show=" + show + " hidden=" + _settings.Hidden
                + " fullscreen=" + _hiddenForFullScreen + " beforeVisible=" + _icon.Visible);

            try
            {
                if (show)
                {
                    if (!_icon.Visible) _icon.Show();
                }
                else
                {
                    _bubble.HideBubble();
                    _panel.HidePanel();
                    if (_icon.Visible) _icon.Hide();
                }
            }
            catch (Exception ex)
            {
                Log.Error("ApplyIconVisibility", ex);
            }

            Program.Trace("apply visibility afterVisible=" + _icon.Visible);
        }

        private void OnIconPressed(object sender, EventArgs e)
        {
            _sawPanelVisible = _panel.Visible;
        }

        private void OnIconClicked(object sender, EventArgs e)
        {
            _bubble.HideBubble();
            if (_sawPanelVisible) _panel.HidePanel();
            else _panel.ShowPanel(Anchor);
        }

        private void OnIconDragFinished(object sender, EventArgs e)
        {
            _settings.HasPosition = true;
            _settings.IconX = _icon.Left;
            _settings.IconY = _icon.Top;
            _settings.Save();
        }

        private void OnBubbleClicked(object sender, EventArgs e)
        {
            _bubble.HideBubble();
            _panel.ShowPanel(Anchor);
        }

        private void OnItemActivated(object sender, ClipItem item)
        {
            if (item == null) return;

            if (!item.CanRestore)
            {
                _panel.HidePanel();
                _bubble.ShowNote("这条图片没有保存实体，无法回填", Anchor);
                return;
            }

            string text = item.RestoreText;
            if (!ClipboardWatcher.SetText(text))
            {
                _bubble.ShowNote("写回剪贴板失败，请稍后再试", Anchor);
                return;
            }

            _suppressUntil = DateTime.UtcNow.AddMilliseconds(400);
            _lastSeq = Native.GetClipboardSequenceNumber();
            _panel.HidePanel();
            _bubble.ShowNote("已复制回剪贴板", Anchor);
        }

        private void TogglePanel()
        {
            _bubble.HideBubble();
            if (_panel.Visible) _panel.HidePanel();
            else _panel.ShowPanel(Anchor);
        }

        private void OnHotKey(object sender, EventArgs e)
        {
            TogglePanel();
        }

        private void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;

            ToolStripMenuItem openItem = new ToolStripMenuItem("打开剪贴历史      Ctrl+Alt+V");
            openItem.Click += delegate { TogglePanel(); };

            _pauseItem = new ToolStripMenuItem("暂停监听");
            _pauseItem.Click += delegate
            {
                _settings.Paused = !_settings.Paused;
                _settings.Save();
            };

            _autoItem = new ToolStripMenuItem("开机自启");
            _autoItem.Click += delegate
            {
                _settings.AutoStart = !_settings.AutoStart;
                _settings.Save();
                if (_settings.AutoStart) Autostart.Enable();
                else Autostart.Disable();
            };

            _hideItem = new ToolStripMenuItem("隐藏浮动图标");
            _hideItem.Click += delegate
            {
                _settings.Hidden = !_settings.Hidden;
                _settings.Save();
                ApplyIconVisibility();
            };

            ToolStripMenuItem resetItem = new ToolStripMenuItem("恢复默认位置");
            resetItem.Click += delegate
            {
                _settings.Hidden = false;
                _settings.HasPosition = false;
                _settings.Save();
                ApplyIconVisibility();
                _icon.PlaceDefault();
                _icon.ClampToScreen();
            };

            _clearItem = new ToolStripMenuItem("清空历史");
            _clearItem.Click += delegate { _history.Clear(); };

            ToolStripMenuItem openDataItem = new ToolStripMenuItem("打开数据目录");
            openDataItem.Click += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + _directory + "\"");
                }
                catch (Exception ex)
                {
                    Log.Error("OpenDataDirectory", ex);
                }
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };

            _menu.Items.Add(openItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_pauseItem);
            _menu.Items.Add(_autoItem);
            _menu.Items.Add(_hideItem);
            _menu.Items.Add(resetItem);
            _menu.Items.Add(openDataItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_clearItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            _menu.Opening += delegate { SyncMenu(); };
        }

        private void SyncMenu()
        {
            _pauseItem.Checked = _settings.Paused;
            _autoItem.Checked = _settings.AutoStart;
            _hideItem.Text = _settings.Hidden ? "显示浮动图标" : "隐藏浮动图标";
            _clearItem.Enabled = _history.Items.Count > 0;
        }

        private void EnsureAutostart()
        {
            try
            {
                if (_settings.AutoStart && !Autostart.IsEnabled()) Autostart.Enable();
                if (!_settings.AutoStart && Autostart.IsEnabled()) Autostart.Disable();
            }
            catch (Exception ex)
            {
                Log.Error("EnsureAutostart", ex);
            }
        }

        internal void ExitApp()
        {
            if (_quitWait != null) { _quitWait.Unregister(null); _quitWait = null; }
            Shutdown();
            ExitThread();
        }

        internal void Shutdown()
        {
            try
            {
                if (_clipTimer != null) { _clipTimer.Stop(); _clipTimer.Dispose(); }
                if (_fullScreenTimer != null) { _fullScreenTimer.Stop(); _fullScreenTimer.Dispose(); }

                if (_hotKeyRegistered && _hotKeyWindow != null)
                {
                    Native.UnregisterHotKey(_hotKeyWindow.Handle, HotKeyId);
                    _hotKeyRegistered = false;
                }
                if (_hotKeyWindow != null) _hotKeyWindow.DestroyHandle();

                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }

                if (_settings != null)
                {
                    if (_icon != null && _icon.Visible)
                    {
                        _settings.HasPosition = true;
                        _settings.IconX = _icon.Left;
                        _settings.IconY = _icon.Top;
                    }
                    _settings.Save();
                }

                if (_icon != null) { _icon.DisposeBitmaps(); _icon.Dispose(); }
                if (_bubble != null) { _bubble.DisposeFonts(); _bubble.Dispose(); }
                if (_panel != null) { _panel.DisposeResources(); _panel.Dispose(); }
                if (_menu != null) _menu.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Shutdown", ex);
            }
        }

        // ---------- 渲染预览（仅开发自检用） ----------

        private static void Pump(int milliseconds)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < until)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
        }

        private static void Capture(Rectangle bounds, string path)
        {
            try
            {
                int width = Math.Max(1, bounds.Width);
                int height = Math.Max(1, bounds.Height);
                using (Bitmap bmp = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Size(width, height));
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                Program.Trace("captured " + path + " " + width + "x" + height);
            }
            catch (Exception ex)
            {
                Log.Error("Capture", ex);
            }
        }

        private static Bitmap SampleImage()
        {
            Bitmap bmp = new Bitmap(400, 260);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                using (System.Drawing.Drawing2D.LinearGradientBrush brush =
                    new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Rectangle(0, 0, 400, 260),
                        Color.FromArgb(255, 66, 133, 244),
                        Color.FromArgb(255, 219, 68, 55), 45f))
                {
                    g.FillRectangle(brush, 0, 0, 400, 260);
                }
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                {
                    g.FillEllipse(brush, 250, 40, 110, 110);
                }
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                {
                    g.FillRectangle(brush, 0, 200, 400, 60);
                }
            }
            return bmp;
        }

        internal void Preview(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);

                ClipItem a = MakeText("这是一段比较长的复制内容，用来验证气泡里的两行预览是否正常、超长时是否会出现省略号，以及右上角那个勾选标记。");
                ClipItem b = MakeText("SELECT * FROM users WHERE created_at > '2025-01-01';");
                ClipItem c = new ClipItem();
                c.Kind = ClipKind.Files;
                c.Files = new string[] { @"C:\Users\me\Desktop\季度报告.pdf", @"C:\Users\me\Desktop\预算表.xlsx" };
                c.Time = DateTime.Now;
                ClipItem d = new ClipItem();
                d.Kind = ClipKind.Image;
                d.ImgW = 1920;
                d.ImgH = 1080;
                d.ImgBytes = 412000;
                d.Time = DateTime.Now;

                _history.Items.Clear();
                _history.Items.Add(a);
                _history.Items.Add(b);
                _history.Items.Add(c);
                _history.Items.Add(d);

                Rectangle anchor = new Rectangle(Ui.Px(45), Ui.Px(60), Ui.Px(48), Ui.Px(48));
                _hasAnchorOverride = true;
                _anchorOverride = anchor;

                _panel.ShowPanel(anchor);
                Pump(500);
                Capture(_panel.Bounds, Path.Combine(directory, "panel.png"));
                _panel.HidePanel();
                Pump(200);

                Rectangle north = new Rectangle(
                    Screen.PrimaryScreen.WorkingArea.Right - Ui.Px(460), Ui.Px(130), Ui.Px(48), Ui.Px(48));
                _bubble.ShowItem(a, north);
                _bubble.Opacity = 1.0;
                Pump(500);
                Capture(_bubble.Bounds, Path.Combine(directory, "bubble-text.png"));
                _bubble.HideBubble();
                Pump(200);

                d.Preview = SampleImage();
                Rectangle north2 = new Rectangle(
                    Screen.PrimaryScreen.WorkingArea.Right - Ui.Px(460), Ui.Px(235), Ui.Px(48), Ui.Px(48));
                _bubble.ShowItem(d, north2);
                _bubble.Opacity = 1.0;
                Pump(500);
                Capture(_bubble.Bounds, Path.Combine(directory, "bubble-image.png"));
                _bubble.HideBubble();
                Pump(200);

                _icon.Location = new Point(
                    Screen.PrimaryScreen.WorkingArea.Right - Ui.Px(110), Ui.Px(90));
                _icon.Show();
                Pump(400);
                Program.Trace("preview icon bounds=" + _icon.Bounds);
                Capture(_icon.Bounds, Path.Combine(directory, "icon.png"));
                _icon.Hide();

                Program.Trace("preview done");
            }
            catch (Exception ex)
            {
                Log.Error("Preview", ex);
            }
        }

        // ---------- 自测 ----------

        private static ClipItem MakeText(string text)
        {
            ClipItem item = new ClipItem();
            item.Kind = ClipKind.Text;
            item.Text = text;
            item.Chars = text.Length;
            item.Time = DateTime.Now;
            return item;
        }

        internal int SelfTest(string resultPath)
        {
            StringBuilder output = new StringBuilder();
            int fails = 0;

            Action<string, bool> check = delegate(string name, bool ok)
            {
                if (!ok) fails++;
                output.AppendLine((ok ? "PASS " : "FAIL ") + name);
            };

            string backup = null;
            bool hadText = false;
            try
            {
                string existing;
                if (ClipboardWatcher.TryGetText(out existing) && !string.IsNullOrEmpty(existing))
                {
                    backup = existing;
                    hadText = true;
                }
            }
            catch
            {
            }

            try
            {
                Program.Trace("selftest: history");
                _history.Clear();
                _history.Add(MakeText("hello"));
                _history.Add(MakeText("world"));
                _history.Add(MakeText("hello"));
                check("history.dedupe+moveToTop",
                    _history.Items.Count == 2 && _history.Items[0].Text == "hello");

                _history.Save();
                HistoryStore reloaded = new HistoryStore(_history.Path_);
                if (reloaded.Items.Count != 2 || reloaded.Items[0].Text != "hello")
                {
                    Program.Trace("persist: path=" + _history.Path_ + " exists=" + File.Exists(_history.Path_)
                        + " count=" + reloaded.Items.Count);
                    try
                    {
                        Program.Trace("persist: raw=" + File.ReadAllText(_history.Path_));
                    }
                    catch (Exception readError)
                    {
                        Program.Trace("persist: read failed " + readError.Message);
                    }
                }
                check("history.persist", reloaded.Items.Count == 2 && reloaded.Items[0].Text == "hello");

                Program.Trace("selftest: clipboard capture");
                string probe = "selftest-" + Guid.NewGuid().ToString("N");
                bool wroteProbe = ClipboardWatcher.SetText(probe);
                check("clipboard.write", wroteProbe);
                Program.Trace("selftest: clipboard set ok=" + wroteProbe);
                _lastSeq = Native.GetClipboardSequenceNumber();
                _suppressUntil = DateTime.MinValue;
                bool captured = ProcessClipboard();
                check("clipboard.capture", captured && _history.Items[0].Text == probe);

                _hasAnchorOverride = true;
                _anchorOverride = new Rectangle(Ui.Px(300), Ui.Px(220), Ui.Px(48), Ui.Px(48));

                Program.Trace("selftest: bubble");
                _bubble.ShowItem(_history.Items[0], _anchorOverride);
                check("bubble.show", _bubble.Visible && _bubble.Width > Ui.Px(200) && _bubble.Height > Ui.Px(40));
                _bubble.HideBubble();
                check("bubble.hide", !_bubble.Visible);

                Program.Trace("selftest: panel");
                _panel.ShowPanel(_anchorOverride);
                check("panel.show", _panel.Visible && _panel.Width == _panel.PanelWidth);
                _panel.HidePanel();
                check("panel.hide", !_panel.Visible);

                Program.Trace("selftest: restore");
                bool wrote = ClipboardWatcher.SetText("restore-check");
                string roundTrip;
                ClipboardWatcher.TryGetText(out roundTrip);
                check("clipboard.restore", wrote && roundTrip == "restore-check");

                ClipItem imageItem = new ClipItem();
                imageItem.Kind = ClipKind.Image;
                imageItem.ImgW = 800;
                imageItem.ImgH = 600;
                check("image.notRestorable", !imageItem.CanRestore);

                output.AppendLine("SKIP sensitive-flag (需真实密码管理器触发)");
                output.AppendLine("SKIP hotkey (需真实按键)");
            }
            catch (Exception ex)
            {
                fails++;
                output.AppendLine("FAIL exception: " + ex);
                Log.Error("SelfTest", ex);
            }
            finally
            {
                try
                {
                    if (hadText && backup != null) ClipboardWatcher.SetText(backup);
                }
                catch
                {
                }
            }

            output.AppendLine(fails == 0 ? "RESULT OK" : "RESULT FAIL " + fails);

            try
            {
                File.WriteAllText(resultPath, output.ToString());
            }
            catch
            {
            }

            return fails == 0 ? 0 : 1;
        }
    }
}
