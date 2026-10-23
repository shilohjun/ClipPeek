using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
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

        private readonly string _directory;

        private Settings _settings;
        private HistoryStore _history;
        private IconForm _icon;
        private BubbleForm _bubble;
        private CursorGlow _cursorGlow;
        private HistoryForm _panel;
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _pauseItem;
        private ToolStripMenuItem _autoItem;
        private ToolStripMenuItem _hideItem;
        private ToolStripMenuItem _clearItem;
        private ToolStripMenuItem _introItem;
        private ToolStripMenuItem _adjustIconItem;
        private ToolStripMenuItem _cropIconItem;
        private ToolStripMenuItem _resetIconItem;
        private ToolStripMenuItem _unsnapItem;
        private ToolStripMenuItem _glowItem;
        private ToolStripMenuItem _autoSnapItem;

        private IntroForm _intro;

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
            _icon.ImageDropped += OnImageDropped;

            _bubble = new BubbleForm();
            _bubble.Clicked += OnBubbleClicked;

            _cursorGlow = new CursorGlow();

            _panel = new HistoryForm(_history);
            _panel.ItemActivated += OnItemActivated;
            _panel.ImageDropped += OnImageDropped;
            _panel.PositionChanged += OnPanelPositionChanged;
            _panel.ShowIconRequested += OnShowIconRequested;

            BuildMenu();
            _icon.IconMenu = _menu;

            PositionIcon();
        }

        internal void Start()
        {
            if (_started) return;
            _started = true;

            LoadCustomIcon();

            _icon.Show();
            ApplyIconVisibility();
            _icon.RebuildBitmaps();
            Program.Trace("start: icon shown at " + _icon.Left + "," + _icon.Top);

            if (!Paths.CanWrite(_directory))
            {
                Log.Error("DataDirectoryNotWritable", new Exception(_directory));
                _bubble.ShowNote("数据目录不可写，历史和图标都存不下来：" + _directory, Anchor);
            }

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

            if (_settings.PlayIntro) PlayIntro();

            if (_settings.Hidden)
            {
                // 上次退出时图标是隐藏状态：启动就告诉用户怎么找回来
                Rectangle trayCorner = new Rectangle(
                    Screen.PrimaryScreen.WorkingArea.Right - Ui.Px(120),
                    Screen.PrimaryScreen.WorkingArea.Bottom - Ui.Px(16),
                    Ui.Px(48), Ui.Px(48));
                _bubble.ShowNote("ClipPeek 在后台运行 · 双击托盘图标可让图标重新出现", trayCorner);
            }
        }

        private void LoadCustomIcon()
        {
            try
            {
                if (!IconImage.Exists(_directory)) return;
                Bitmap custom = IconImage.Load(IconImage.Path_(_directory));
                if (custom != null) Theme.SetCustomIcon(custom);
            }
            catch (Exception ex)
            {
                Log.Error("LoadCustomIcon", ex);
            }
        }

        private void ReloadIconBitmaps()
        {
            try
            {
                _icon.RebuildBitmaps();
                if (_tray != null)
                {
                    Icon old = _tray.Icon;
                    _tray.Icon = Theme.MakeIcon(Ui.Px(32));
                    if (old != null) old.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error("ReloadIconBitmaps", ex);
            }
        }

        internal void PlayIntro()
        {
            if (_intro != null) return;

            try
            {
                if (_settings.Hidden || _hiddenForFullScreen) return;

                Rectangle target = _icon.Bounds;
                Bitmap icon = Theme.BuildBitmap(target.Width, false);

                _icon.Hide();

                IntroForm intro = new IntroForm(icon, target);
                _intro = intro;
                intro.Finished += delegate
                {
                    _intro = null;
                    try
                    {
                        if (!_settings.Hidden && !_hiddenForFullScreen) _icon.Show();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("PlayIntro.ShowIcon", ex);
                    }
                    if (icon != null) icon.Dispose();
                    intro.Dispose();
                };
                intro.Begin();
                Program.Trace("intro started");
            }
            catch (Exception ex)
            {
                Log.Error("PlayIntro", ex);
                if (_intro != null) { _intro.Dispose(); _intro = null; }
                try { if (!_settings.Hidden && !_hiddenForFullScreen) _icon.Show(); } catch { }
            }
        }

        private void SkipIntro()
        {
            if (_intro != null) _intro.Skip();
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

            if (_settings.Snap != SnapEdge.None) _icon.DockTo(_settings.Snap);
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
            if (_settings.CursorGlow && !_hiddenForFullScreen) _cursorGlow.Play();
            Program.Trace("clip captured: kind=" + item.Kind + " chars=" + item.Chars + " truncated=" + item.Truncated);

            // 超长文本也弹气泡——没反馈会被当成「没复制上」；截断情况由气泡第二行说明

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
            else ShowPanelNow();
        }

        private void OnIconDragFinished(object sender, EventArgs e)
        {
            _settings.HasPosition = true;
            _settings.IconX = _icon.Left;
            _settings.IconY = _icon.Top;

            SnapEdge edge = _settings.AutoSnap ? FindNearestEdge(_icon.Bounds) : SnapEdge.None;
            if (edge != SnapEdge.None) _icon.DockTo(edge);
            _settings.Snap = _icon.Edge;
            _settings.Save();

            // 面板默认跟着图标走：图标拖完，开着的话面板一起挪
            if (_panel.Visible) _panel.FollowIcon(_icon.Bounds);
        }

        /// <summary>拖到离屏幕边缘足够近就吸附过去。</summary>
        private static SnapEdge FindNearestEdge(Rectangle bounds)
        {
            int threshold = Ui.Px(24);
            Screen screen = Screen.FromRectangle(bounds);
            Rectangle work = screen.WorkingArea;

            int left = bounds.Left - work.Left;
            int top = bounds.Top - work.Top;
            int right = work.Right - bounds.Right;
            int bottom = work.Bottom - bounds.Bottom;

            int best = Math.Min(Math.Min(left, top), Math.Min(right, bottom));
            if (best > threshold) return SnapEdge.None;

            if (best == left) return SnapEdge.Left;
            if (best == top) return SnapEdge.Top;
            if (best == right) return SnapEdge.Right;
            return SnapEdge.Bottom;
        }

        private void OnBubbleClicked(object sender, EventArgs e)
        {
            _bubble.HideBubble();
            ShowPanelNow();
        }

        private void OnPanelPositionChanged(object sender, EventArgs e)
        {
            try
            {
                _settings.HasPanelPosition = true;
                _settings.PanelX = _panel.Left;
                _settings.PanelY = _panel.Top;
                _settings.Save();
            }
            catch (Exception ex)
            {
                Log.Error("OnPanelPositionChanged", ex);
            }
        }

        private void OnShowIconRequested(object sender, EventArgs e)
        {
            try
            {
                _settings.Hidden = false;
                _settings.Save();
                ApplyIconVisibility();
                _panel.HidePanel();
                _bubble.ShowNote("图标回来了 · 直接拖动它就能换位置", Anchor);
            }
            catch (Exception ex)
            {
                Log.Error("OnShowIconRequested", ex);
            }
        }

        /// <summary>
        /// 统一的「打开历史面板」入口：
        /// 图标在的时候永远贴着图标；只有图标被隐藏了，才用面板自己记住的位置。
        /// </summary>
        private void ShowPanelNow()
        {
            _panel.IconHidden = _settings.Hidden;
            bool useManual = _settings.Hidden && _settings.HasPanelPosition;
            _panel.ShowPanel(Anchor, useManual, new Point(_settings.PanelX, _settings.PanelY));
        }

        private Rectangle CursorAnchor()
        {
            int size = Ui.Px(48);
            Point point = Cursor.Position;
            return new Rectangle(point.X - size / 2, point.Y - size / 2, size, size);
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

            if (item.Kind == ClipKind.Files)
            {
                RestoreFiles(item);
                return;
            }

            string text = item.RestoreText;
            if (!ClipboardWatcher.SetText(text))
            {
                _bubble.ShowNote("写回剪贴板失败，请稍后再试", Anchor);
                return;
            }

            MarkClipboardRestored();
            _panel.HidePanel();
            _bubble.ShowNote("已复制回剪贴板", Anchor);
        }

        /// <summary>
        /// 文件类记录回填：优先放回真正的文件（CF_HDROP），文件还在的话资源管理器里能直接粘贴。
        /// 文件都不在了，就退回只复制路径，并明确告诉用户。
        /// </summary>
        private void RestoreFiles(ClipItem item)
        {
            int existing;
            int total;
            bool restored = ClipboardWatcher.SetFiles(item.Files, out existing, out total);

            if (restored)
            {
                MarkClipboardRestored();
                _panel.HidePanel();
                _bubble.ShowNote(
                    existing >= total
                        ? "已放回 " + existing + " 个文件"
                        : "已放回 " + existing + " / " + total + " 个文件（其余已不在原处）",
                    Anchor);
                return;
            }

            string paths = item.RestoreText;
            if (!ClipboardWatcher.SetText(paths))
            {
                _bubble.ShowNote("写回剪贴板失败，请稍后再试", Anchor);
                return;
            }

            MarkClipboardRestored();
            _panel.HidePanel();
            _bubble.ShowNote("文件已不在原处，这次只复制到了路径", Anchor);
        }

        private void MarkClipboardRestored()
        {
            _suppressUntil = DateTime.UtcNow.AddMilliseconds(400);
            _lastSeq = Native.GetClipboardSequenceNumber();
        }

        private void TogglePanel()
        {
            SkipIntro();
            _bubble.HideBubble();
            if (_panel.Visible) _panel.HidePanel();
            else ShowPanelNow();
        }

        private void OnHotKey(object sender, EventArgs e)
        {
            TogglePanel();
        }

        private void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            // 没有这一行，Checked 的勾根本画不出来
            _menu.ShowCheckMargin = true;
            _menu.ShowItemToolTips = true;

            ToolStripMenuItem openItem = new ToolStripMenuItem("打开剪贴历史      Ctrl+Alt+V");
            openItem.Click += delegate { TogglePanel(); };

            _pauseItem = new ToolStripMenuItem("暂停监听");
            _pauseItem.CheckOnClick = true;
            _pauseItem.Click += delegate
            {
                _settings.Paused = _pauseItem.Checked;
                _settings.Save();
            };

            _autoItem = new ToolStripMenuItem("开机自启");
            _autoItem.CheckOnClick = true;
            _autoItem.Click += delegate
            {
                _settings.AutoStart = _autoItem.Checked;
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

                if (_settings.Hidden)
                {
                    _bubble.ShowNote("图标已隐藏 · 双击托盘图标，或按 Ctrl+Alt+V 打开历史面板", CursorAnchor());
                }
            };

            ToolStripMenuItem resetItem = new ToolStripMenuItem("恢复默认位置");
            resetItem.Click += delegate
            {
                _settings.Hidden = false;
                _settings.HasPosition = false;
                _settings.HasPanelPosition = false;
                _settings.Snap = SnapEdge.None;
                _settings.Save();
                _icon.Undock();
                ApplyIconVisibility();
                _icon.PlaceDefault();
                _icon.ClampToScreen();
            };

            _unsnapItem = new ToolStripMenuItem("取消吸附");
            _unsnapItem.Click += delegate
            {
                _icon.Undock();
                _settings.Snap = SnapEdge.None;
                _settings.HasPosition = true;
                _settings.IconX = _icon.Left;
                _settings.IconY = _icon.Top;
                _settings.Save();
                _bubble.ShowNote("已取消吸附", Anchor);
            };

            _autoSnapItem = new ToolStripMenuItem("自动吸附屏幕边缘");
            _autoSnapItem.CheckOnClick = true;
            _autoSnapItem.Click += delegate
            {
                _settings.AutoSnap = _autoSnapItem.Checked;
                _settings.Save();

                if (!_settings.AutoSnap && _icon.IsDocked)
                {
                    _icon.Undock();
                    _settings.Snap = SnapEdge.None;
                    _settings.HasPosition = true;
                    _settings.IconX = _icon.Left;
                    _settings.IconY = _icon.Top;
                    _settings.Save();
                    _bubble.ShowNote("已关闭自动吸附，图标恢复完整", Anchor);
                }
            };

            _clearItem = new ToolStripMenuItem("清空历史");
            _clearItem.Click += delegate { _history.Clear(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApp(); };

            _adjustIconItem = new ToolStripMenuItem("更换图标…（也可直接拖图片到图标上）");
            _adjustIconItem.ToolTipText = "选一张图片，或者直接把图片拖到浮动图标上";
            _adjustIconItem.Click += delegate { ChooseIconFile(); };

            _cropIconItem = new ToolStripMenuItem("调整图标裁剪…");
            _cropIconItem.Click += delegate { AdjustIcon(); };

            _resetIconItem = new ToolStripMenuItem("恢复默认图标");
            _resetIconItem.Click += delegate { ResetIcon(); };

            _introItem = new ToolStripMenuItem("启动时播开场动画");
            _introItem.CheckOnClick = true;
            _introItem.Click += delegate
            {
                _settings.PlayIntro = _introItem.Checked;
                _settings.Save();
            };

            _glowItem = new ToolStripMenuItem("复制时光标光晕");
            _glowItem.CheckOnClick = true;
            _glowItem.Click += delegate
            {
                _settings.CursorGlow = _glowItem.Checked;
                _settings.Save();
                if (!_settings.CursorGlow) _cursorGlow.Stop();
            };

            ToolStripMenuItem replayItem = new ToolStripMenuItem("重播开场动画");
            replayItem.Click += delegate { PlayIntro(); };

            ToolStripMenuItem shortcutItem = new ToolStripMenuItem("创建开始菜单快捷方式（Ctrl+Alt+C）");
            shortcutItem.Click += delegate { CreateStartMenuShortcut(); };

            _menu.Items.Add(openItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_pauseItem);
            _menu.Items.Add(_autoItem);
            _menu.Items.Add(_hideItem);
            _menu.Items.Add(resetItem);
            _menu.Items.Add(_autoSnapItem);
            _menu.Items.Add(_unsnapItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_adjustIconItem);
            _menu.Items.Add(_cropIconItem);
            _menu.Items.Add(_resetIconItem);
            _menu.Items.Add(_introItem);
            _menu.Items.Add(_glowItem);
            _menu.Items.Add(replayItem);
            _menu.Items.Add(shortcutItem);
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
            _unsnapItem.Enabled = _icon.IsDocked;
            _introItem.Checked = _settings.PlayIntro;
            _glowItem.Checked = _settings.CursorGlow;
            _autoSnapItem.Checked = _settings.AutoSnap;
            _resetIconItem.Enabled = Theme.CustomIcon != null;
            _cropIconItem.Enabled = Theme.CustomIcon != null;
        }

        // ---------- 自定义图标 ----------

        private void OnImageDropped(object sender, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                SkipIntro();
                SetCustomIconFrom(path);
            }
            catch (Exception ex)
            {
                Log.Error("OnImageDropped", ex);
                _bubble.ShowNote("这张图片读不了，换一张试试", Anchor);
            }
        }

        private void SetCustomIconFrom(string path)
        {
            try
            {
                SetCustomIconCore(path);
            }
            catch (Exception ex)
            {
                Log.Error("SetCustomIconFrom", ex);
                _bubble.ShowNote("换图标失败：" + ex.Message, Anchor);
            }
        }

        private void SetCustomIconCore(string path)
        {
            using (IconCropForm dialog = new IconCropForm(path, _directory))
            {
                DialogResult result = dialog.ShowDialog();
                if (result != DialogResult.OK) return;

                if (dialog.ResetRequested)
                {
                    ReloadIconBitmaps();
                    _bubble.ShowNote("已恢复默认图标", Anchor);
                    return;
                }

                Bitmap custom = IconImage.Load(IconImage.Path_(_directory));
                if (custom == null)
                {
                    _bubble.ShowNote("图标没存下来（数据目录不可写）：" + _directory, Anchor);
                    return;
                }

                Theme.SetCustomIcon(custom);
                ReloadIconBitmaps();
                _bubble.ShowNote("图标已更换", Anchor);
            }
        }

        private void AdjustIcon()
        {
            if (Theme.CustomIcon != null && IconImage.Exists(_directory))
            {
                SetCustomIconFrom(IconImage.Path_(_directory));
                return;
            }

            _bubble.ShowNote("把一张图片拖到图标上就能换", Anchor);
        }

        private void ChooseIconFile()
        {
            try
            {
                SkipIntro();
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "选择一张图片作为 ClipPeek 的图标（也可以直接把图片拖到浮动图标上）";
                    dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif;*.tif;*.tiff;*.webp|所有文件|*.*";
                    dialog.CheckFileExists = true;
                    dialog.RestoreDirectory = true;
                    if (dialog.ShowDialog() != DialogResult.OK) return;
                    SetCustomIconFrom(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                Log.Error("ChooseIconFile", ex);
            }
        }

        private void ResetIcon()
        {
            try
            {
                IconImage.Delete(_directory);
                Theme.SetCustomIcon(null);
                ReloadIconBitmaps();
                _bubble.ShowNote("已恢复默认图标", Anchor);
            }
            catch (Exception ex)
            {
                Log.Error("ResetIcon", ex);
            }
        }

        // ---------- 没在运行时怎么快速打开 ----------

        private void CreateStartMenuShortcut()
        {
            try
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string path = Path.Combine(programs, "ClipPeek.lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });

                Type shortcutType = shortcut.GetType();
                string exe = Application.ExecutablePath;
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Path.GetDirectoryName(exe) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "ClipPeek 剪贴板气泡与历史" });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { exe + ",0" });
                shortcutType.InvokeMember("ShortcutKey", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "CTRL+ALT+C" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                _bubble.ShowNote("已创建开始菜单快捷方式，Ctrl+Alt+C 启动", Anchor);
            }
            catch (Exception ex)
            {
                Log.Error("CreateStartMenuShortcut", ex);
                _bubble.ShowNote("创建快捷方式失败（可能没有权限）", Anchor);
            }
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
            if (_cursorGlow != null) { _cursorGlow.Dispose(); }
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

                _panel.ShowPanel(anchor, false, Point.Empty);
                Pump(500);
                Capture(_panel.Bounds, Path.Combine(directory, "panel.png"));
                _panel.HidePanel();
                Pump(200);

                _panel.IconHidden = true;
                _panel.ShowPanel(anchor, false, Point.Empty);
                Pump(400);
                Capture(_panel.Bounds, Path.Combine(directory, "panel-hidden.png"));
                _panel.HidePanel();
                _panel.IconHidden = false;
                // 右键菜单：验证勾选项真的画得出来
                _menu.Show(new Point(Ui.Px(120), Ui.Px(120)));
                Pump(400);
                Capture(_menu.Bounds, Path.Combine(directory, "menu.png"));
                _menu.Close();
                Pump(200);
                Pump(200);

                Rectangle north = new Rectangle(
                    Screen.PrimaryScreen.WorkingArea.Right - Ui.Px(460), Ui.Px(130), Ui.Px(48), Ui.Px(48));
                _bubble.ShowItem(a, north);
                _bubble.Opacity = 1.0;
                Pump(500);
                Capture(_bubble.Bounds, Path.Combine(directory, "bubble-text.png"));
                _bubble.HideBubble();
                Pump(200);

                // 超长文本被截断时的气泡（内容 + 截断说明两行）
                ClipItem truncated = MakeText(new string('x', 120) + " 这是一段被截断的超长文本");
                truncated.Chars = 312480;
                truncated.Truncated = true;
                _bubble.ShowItem(truncated, north);
                _bubble.Opacity = 1.0;
                Pump(500);
                Capture(_bubble.Bounds, Path.Combine(directory, "bubble-truncated.png"));
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

                RenderIntroFrames(directory);
                RenderDockFrames(directory);

                // 换图标对话框的截图（顺便验证它能正常渲染）
                string samplePath = Path.Combine(directory, "sample-photo.png");
                using (Bitmap sample = SampleImage())
                {
                    sample.Save(samplePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                using (IconCropForm crop = new IconCropForm(samplePath, _directory))
                {
                    crop.Show();
                    Pump(500);
                    Program.Trace("preview crop dialog visible=" + crop.Visible + " bounds=" + crop.Bounds);
                    try
                    {
                        using (Bitmap shot = new Bitmap(crop.Width, crop.Height))
                        {
                            crop.DrawToBitmap(shot, new Rectangle(0, 0, crop.Width, crop.Height));
                            shot.Save(Path.Combine(directory, "crop-dialog.png"),
                                System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                    catch (Exception shotError)
                    {
                        Log.Error("CaptureCropDialog", shotError);
                    }
                    crop.Hide();
                }

                Program.Trace("preview done");
            }
            catch (Exception ex)
            {
                Log.Error("Preview", ex);
            }
        }

        /// <summary>吸附后的样子：半个图标贴在屏幕边缘，按呼吸灯的不同透明度拼一张图。</summary>
        private void RenderDockFrames(string directory)
        {
            try
            {
                int box = Ui.Px(48);
                int half = box / 2;
                int gap = Ui.Px(10);
                double[] phases = new double[] { 0.0, 0.25, 0.5, 0.75 };

                int width = phases.Length * half + (phases.Length + 1) * gap;
                int height = box + gap * 2;

                using (Bitmap strip = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(strip))
                    {
                        g.Clear(Color.FromArgb(255, 240, 243, 248));
                        g.SmoothingMode = SmoothingMode.AntiAlias;

                        using (Bitmap icon = Theme.BuildBitmap(box, false))
                        using (ImageAttributes attributes = new ImageAttributes())
                        {
                            for (int i = 0; i < phases.Length; i++)
                            {
                                Rectangle cell = new Rectangle(gap + i * (half + gap), gap, half, box);
                                using (SolidBrush cellBg = new SolidBrush(Color.FromArgb(255, 252, 253, 255)))
                                {
                                    g.FillRectangle(cellBg, cell);
                                }

                                double opacity = DockBreath.OpacityAt(phases[i] * DockBreath.PeriodMs);
                                ColorMatrix matrix = new ColorMatrix();
                                matrix.Matrix00 = 1f;
                                matrix.Matrix11 = 1f;
                                matrix.Matrix22 = 1f;
                                matrix.Matrix33 = (float)opacity;
                                matrix.Matrix44 = 1f;
                                attributes.SetColorMatrix(matrix);

                                // 每个格子左边缘当作屏幕边缘：只露图标右半边
                                Region old = g.Clip;
                                g.SetClip(cell);
                                g.DrawImage(icon,
                                    new Rectangle(cell.X - half, cell.Y, box, box),
                                    0, 0, box, box, GraphicsUnit.Pixel, attributes);
                                g.Clip = old;
                            }
                        }
                    }

                    strip.Save(Path.Combine(directory, "dock-frames.png"), System.Drawing.Imaging.ImageFormat.Png);
                }

                Program.Trace("preview dock frames done");
            }
            catch (Exception ex)
            {
                Log.Error("RenderDockFrames", ex);
            }
        }

        /// <summary>把开场动画的关键帧离线渲染成 PNG（不依赖屏幕是否亮着）。</summary>
        private void RenderIntroFrames(string directory)
        {
            try
            {
                int box = Ui.Px(48);
                int canvas = box * 3;
                int radius = Ui.Px(24);

                Bitmap icon = Theme.BuildBitmap(box, false);
                IntroAnimation animation = new IntroAnimation(icon, box, canvas, Ui.Scale, new Random(7));

                int[] frames = new int[]
                {
                    (int)(animation.TotalMs * 0.12),
                    (int)(animation.TotalMs * 0.30),
                    (int)(animation.TotalMs * 0.50),
                    (int)(animation.TotalMs * 0.72),
                    animation.SettleMs + 40
                };

                for (int i = 0; i < frames.Length; i++)
                {
                    using (Bitmap frame = new Bitmap(canvas, canvas))
                    {
                        using (Graphics g = Graphics.FromImage(frame))
                        {
                            // 预览图给一块深色底，真实运行时这里是你的桌面（窗口是透明的）
                            using (LinearGradientBrush backdrop = new LinearGradientBrush(
                                new Rectangle(0, 0, canvas, canvas),
                                Color.FromArgb(255, 26, 30, 38),
                                Color.FromArgb(255, 44, 52, 66),
                                35f))
                            {
                                g.FillRectangle(backdrop, 0, 0, canvas, canvas);
                            }

                            animation.Draw(g, frames[i]);
                        }

                        frame.Save(Path.Combine(directory, "intro-" + (i + 1) + ".png"),
                            System.Drawing.Imaging.ImageFormat.Png);
                    }
                }

                icon.Dispose();
                Program.Trace("preview intro frames done");
            }
            catch (Exception ex)
            {
                Log.Error("RenderIntroFrames", ex);
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

                // 超长文本：内容按上限截断，但气泡照样弹，并在第二行说明截断
                string huge = new string('x', ClipboardWatcher.MaxTextChars + 5000);
                bool hugeSet = ClipboardWatcher.SetText(huge);
                _lastSeq = Native.GetClipboardSequenceNumber();
                _suppressUntil = DateTime.MinValue;
                bool hugeCaptured = ProcessClipboard();
                ClipItem hugeItem = _history.Items[0];
                check("text.truncatedStorage", hugeSet && hugeCaptured && hugeItem.Truncated
                    && hugeItem.Text.Length == ClipboardWatcher.MaxTextChars
                    && hugeItem.Chars == huge.Length);
                check("text.truncatedBubble", _bubble.Visible
                    && hugeItem.BubbleLines.Length >= 2
                    && hugeItem.BubbleLines[hugeItem.BubbleLines.Length - 1].IndexOf("已截断") >= 0);
                check("text.truncatedSubtitle", hugeItem.Subtitle.IndexOf("已截断") >= 0);
                _bubble.HideBubble();

                _hasAnchorOverride = true;
                _anchorOverride = new Rectangle(Ui.Px(300), Ui.Px(220), Ui.Px(48), Ui.Px(48));

                Program.Trace("selftest: bubble");
                _bubble.ShowItem(_history.Items[0], _anchorOverride);
                check("bubble.show", _bubble.Visible && _bubble.Width > Ui.Px(200) && _bubble.Height > Ui.Px(40));
                _bubble.HideBubble();
                check("bubble.hide", !_bubble.Visible);

                Program.Trace("selftest: panel");
                _panel.ShowPanel(_anchorOverride, false, Point.Empty);
                check("panel.show", _panel.Visible && _panel.Width == _panel.PanelWidth);
                _panel.HidePanel();
                check("panel.hide", !_panel.Visible);

                Program.Trace("selftest: restore");
                bool wrote = ClipboardWatcher.SetText("restore-check");
                string roundTrip;
                ClipboardWatcher.TryGetText(out roundTrip);
                check("clipboard.restore", wrote && roundTrip == "restore-check");

                // 文件回填：放回真正的文件剪贴板，而不是一串路径文本
                string dropFile = Path.Combine(_directory, "selftest-file.txt");
                File.WriteAllText(dropFile, "clipboard file test");
                int existingCount;
                int totalCount;
                bool filesOk = ClipboardWatcher.SetFiles(new string[] { dropFile }, out existingCount, out totalCount);
                bool listed = Clipboard.ContainsFileDropList();
                string firstListed = listed && Clipboard.GetFileDropList().Count > 0
                    ? Clipboard.GetFileDropList()[0]
                    : null;
                check("clipboard.files", filesOk && existingCount == 1 && listed
                    && string.Equals(firstListed, dropFile, StringComparison.OrdinalIgnoreCase));

                // 同时带上一份路径文本，记事本这类只认文本的地方也能粘
                string fileText;
                ClipboardWatcher.TryGetText(out fileText);
                check("clipboard.filesText",
                    !string.IsNullOrEmpty(fileText)
                    && fileText.IndexOf("selftest-file.txt", StringComparison.OrdinalIgnoreCase) >= 0);

                int goneExisting;
                int goneTotal;
                bool goneOk = ClipboardWatcher.SetFiles(
                    new string[] { Path.Combine(_directory, "not-here.zzz") }, out goneExisting, out goneTotal);
                check("clipboard.filesGone", !goneOk && goneExisting == 0 && goneTotal == 1);
                try { File.Delete(dropFile); } catch { }

                ClipItem imageItem = new ClipItem();
                imageItem.Kind = ClipKind.Image;
                imageItem.ImgW = 800;
                imageItem.ImgH = 600;
                check("image.notRestorable", !imageItem.CanRestore);

                // 自定义图标的裁剪渲染
                using (Bitmap sample = SampleImage())
                {
                    using (Bitmap rendered = IconImage.RenderIcon(sample, Ui.Px(96), 1.6f, new PointF(0.4f, 0.6f), Ui.Px(24)))
                    {
                        check("icon.customRender", rendered.Width == Ui.Px(96) && rendered.Height == Ui.Px(96));
                    }
                }
                check("icon.extensionCheck",
                    IconImage.LooksLikeImage("a.png") && IconImage.LooksLikeImage("b.JPG")
                    && !IconImage.LooksLikeImage("c.exe") && !IconImage.LooksLikeImage("d.txt"));

                // 呼吸灯：一轮要够慢，最低/最高透明度在合理区间
                double breathMin = DockBreath.OpacityAt(0);
                double breathMax = DockBreath.OpacityAt(DockBreath.PeriodMs / 2);
                double breathMid = DockBreath.OpacityAt(DockBreath.PeriodMs * 3 / 4);
                check("dock.breath", DockBreath.PeriodMs >= 4000
                    && Math.Abs(breathMin - DockBreath.MinOpacity) < 0.01
                    && Math.Abs(breathMax - DockBreath.MaxOpacity) < 0.01
                    && breathMid > breathMin && breathMid < breathMax);
                // 光标光晕：环从 0.62 扩到 1.0，透明度先快速亮起再淡出
                check("glow.curve", CursorGlow.RadiusScale(0) > 0.60 && CursorGlow.RadiusScale(0) < 0.65
                    && CursorGlow.RadiusScale(1) > 0.99
                    && CursorGlow.OpacityAt(0) < 0.01
                    && CursorGlow.OpacityAt(0.10) > 0.99
                    && CursorGlow.OpacityAt(1) < 0.01);

                // 吸附进出：吸附时窗口真的一半，解除后整枚回到屏幕内
                Rectangle dockWork = Screen.FromPoint(_icon.Bounds.Location).WorkingArea;
                _icon.DockTo(SnapEdge.Right);
                bool dockEntered = _icon.IsDocked
                    && _icon.Width == Ui.Px(48) / 2 && _icon.Height == Ui.Px(48)
                    && _icon.Right == dockWork.Right;
                _icon.Undock();
                bool dockExited = !_icon.IsDocked && _icon.Width == Ui.Px(48)
                    && _icon.Right <= dockWork.Right;
                check("dock.enter+exit", dockEntered && dockExited);

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
