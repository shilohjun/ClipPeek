using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClipPeek
{
    internal static class ClipboardWatcher
    {
        /// <summary>单条文本最多保存这么多字符，超出部分丢弃并标记「已截断」。</summary>
        internal const int MaxTextChars = 50000;

        private const string IgnoreFormat = "Clipboard Viewer Ignore";

        internal static ClipItem Capture(out string error)
        {
            error = null;

            IDataObject data = null;
            for (int attempt = 0; attempt < 12 && data == null; attempt++)
            {
                try
                {
                    data = Clipboard.GetDataObject();
                }
                catch (ExternalException)
                {
                    Thread.Sleep(25);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Thread.Sleep(25);
                }
            }

            if (data == null)
            {
                if (error == null) error = "clipboard busy";
                return null;
            }

            try
            {
                // 密码管理器等会打这个标记，表示内容不希望被记录
                if (data.GetDataPresent(IgnoreFormat, false)) return null;

                if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        ClipItem item = new ClipItem();
                        item.Kind = ClipKind.Files;
                        item.Files = files;
                        item.Time = DateTime.Now;
                        return item;
                    }
                }

                if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
                {
                    string text = data.GetData(DataFormats.UnicodeText) as string;
                    if (text == null) text = data.GetData(DataFormats.Text) as string;

                    if (text != null && text.Trim().Length > 0)
                    {
                        ClipItem item = new ClipItem();
                        item.Kind = ClipKind.Text;
                        item.Chars = text.Length;
                        item.Time = DateTime.Now;
                        if (text.Length > MaxTextChars)
                        {
                            item.Text = text.Substring(0, MaxTextChars);
                            item.Truncated = true;
                        }
                        else
                        {
                            item.Text = text;
                        }
                        return item;
                    }
                }

                if (data.GetDataPresent(DataFormats.Bitmap))
                {
                    object raw = data.GetData(DataFormats.Bitmap);
                    Bitmap bitmap = raw as Bitmap;
                    if (bitmap != null)
                    {
                        ClipItem item = new ClipItem();
                        item.Kind = ClipKind.Image;
                        item.ImgW = bitmap.Width;
                        item.ImgH = bitmap.Height;
                        item.ImgBytes = EstimateBytes(bitmap);
                        item.Time = DateTime.Now;
                        item.Preview = Thumbnail(bitmap, Ui.Px(220), Ui.Px(140));
                        return item;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return null;
        }

        internal static bool SetText(string text)
        {
            if (text == null) return false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    Log.Error("ClipboardWatcher.SetText", ex);
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 把文件放回剪贴板（CF_HDROP），这样在资源管理器里能直接粘贴文件。
        /// 只放仍然存在的路径；返回 false 表示一个都不在了，调用方该退回复制路径。
        /// </summary>
        internal static bool SetFiles(string[] paths, out int existing, out int total)
        {
            existing = 0;
            total = paths == null ? 0 : paths.Length;
            if (total == 0) return false;

            StringCollection list = new StringCollection();
            foreach (string path in paths)
            {
                try
                {
                    if (string.IsNullOrEmpty(path)) continue;
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        list.Add(path);
                        existing++;
                    }
                }
                catch
                {
                }
            }

            if (list.Count == 0) return false;

            string[] selected = new string[list.Count];
            list.CopyTo(selected, 0);

            // 同时给文件（CF_HDROP）和文本两种格式：
            // 资源管理器里粘贴得到文件，记事本这类只认文本的地方粘贴得到路径。
            DataObject data = new DataObject();
            data.SetData(DataFormats.FileDrop, selected);
            data.SetData(DataFormats.UnicodeText, string.Join("\r\n", selected));

            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(data, true);
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    Log.Error("ClipboardWatcher.SetFiles", ex);
                    return false;
                }
            }

            return false;
        }

        internal static bool TryGetText(out string text)
        {
            text = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsText()) return true;
                    text = Clipboard.GetText();
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    Log.Error("ClipboardWatcher.TryGetText", ex);
                    return false;
                }
            }
            return false;
        }

        private static long EstimateBytes(Bitmap bitmap)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Save(stream, ImageFormat.Png);
                    return stream.Length;
                }
            }
            catch
            {
                return ((long)bitmap.Width * bitmap.Height) * 4;
            }
        }

        private static Bitmap Thumbnail(Bitmap source, int maxWidth, int maxHeight)
        {
            try
            {
                double scale = Math.Min(
                    (double)maxWidth / Math.Max(1, source.Width),
                    (double)maxHeight / Math.Max(1, source.Height));
                if (scale > 1.0) scale = 1.0;

                int width = Math.Max(1, (int)(source.Width * scale));
                int height = Math.Max(1, (int)(source.Height * scale));

                Bitmap result = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, width, height);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
