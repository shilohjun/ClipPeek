using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ClipPeek
{
    internal enum ClipKind
    {
        Text = 0,
        Image = 1,
        Files = 2
    }

    internal sealed class ClipItem
    {
        internal ClipKind Kind;
        internal string Text;
        internal int Chars;
        internal int ImgW;
        internal int ImgH;
        internal long ImgBytes;
        internal string[] Files;
        internal DateTime Time;
        internal bool Truncated;

        /// <summary>文件类记录：当初的文件是否已经全部不在了（面板上标出来）。</summary>
        internal bool FilesMissing;

        // 仅用于当场弹出的气泡，不落盘
        internal Bitmap Preview;

        internal string Signature
        {
            get
            {
                if (Kind == ClipKind.Text) return "T|" + (Text ?? string.Empty);
                if (Kind == ClipKind.Image) return "I|" + ImgW + "x" + ImgH;
                return "F|" + string.Join("|", Files ?? new string[0]);
            }
        }

        internal string KindLabel
        {
            get
            {
                if (Kind == ClipKind.Text) return "文本";
                if (Kind == ClipKind.Image) return "图片";
                return "文件";
            }
        }

        internal string TitleText
        {
            get
            {
                if (Kind == ClipKind.Text) return "文本 · " + Chars + " 字";
                if (Kind == ClipKind.Image) return "图片 · " + ImgW + "×" + ImgH;
                int n = Files == null ? 0 : Files.Length;
                return n + " 个文件";
            }
        }

        internal string Subtitle
        {
            get
            {
                string head;
                if (Kind == ClipKind.Text) head = Chars + " 字";
                else if (Kind == ClipKind.Image) head = ImgW + "×" + ImgH;
                else head = (Files == null ? 0 : Files.Length) + " 个文件";
                return head + " · " + Time.ToString("HH:mm:ss");
            }
        }

        internal string[] ContentLines
        {
            get
            {
                List<string> lines = new List<string>();

                if (Kind == ClipKind.Text)
                {
                    string text = (Text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
                    string[] raw = text.Split('\n');
                    foreach (string line in raw)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length == 0) continue;
                        lines.Add(Shorten(trimmed, 120));
                        if (lines.Count == 2) break;
                    }
                    if (lines.Count == 0) lines.Add("(空白文本)");
                }
                else if (Kind == ClipKind.Image)
                {
                    lines.Add("（未保存图片本身，无法回填）");
                }
                else
                {
                    if (Files != null)
                    {
                        foreach (string file in Files)
                        {
                            lines.Add(Shorten(Path.GetFileName(file), 120));
                            if (lines.Count == 2) break;
                        }
                    }
                    if (lines.Count == 0) lines.Add("(空文件列表)");
                }

                return lines.ToArray();
            }
        }

        internal bool CanRestore
        {
            get { return Kind != ClipKind.Image; }
        }

        internal string RestoreText
        {
            get
            {
                if (Kind == ClipKind.Text) return Text ?? string.Empty;
                if (Kind == ClipKind.Files) return Files == null ? string.Empty : string.Join("\r\n", Files);
                return string.Empty;
            }
        }

        internal static string Shorten(string value, int max)
        {
            if (value == null) return string.Empty;
            value = value.Trim();
            if (value.Length <= max) return value;
            return value.Substring(0, max - 1) + "…";
        }

        internal static string Bytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }
    }
}
