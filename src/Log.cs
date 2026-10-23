using System;
using System.IO;

namespace ClipPeek
{
    internal static class Log
    {
        private static string _path;
        private static readonly object Gate = new object();

        internal static bool Verbose;

        internal static void Init(string directory)
        {
            _path = Path.Combine(directory, "clip-peek.log");
        }

        internal static void Error(string where, Exception ex)
        {
            Write("ERROR " + where + ": " + (ex == null ? "(null)" : ex.ToString()));
        }

        internal static void Info(string message)
        {
            if (Verbose) Write("INFO " + message);
        }

        private static void Write(string line)
        {
            try
            {
                lock (Gate)
                {
                    if (_path == null) return;
                    FileInfo fi = new FileInfo(_path);
                    if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line + Environment.NewLine);
                }
            }
            catch
            {
                // logging must never break the app
            }
        }
    }
}
