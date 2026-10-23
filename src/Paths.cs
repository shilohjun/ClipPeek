using System;
using System.IO;

namespace ClipPeek
{
    /// <summary>
    /// 决定数据（history.json / settings.json / clip-peek.log）写在哪里：
    /// 1. 便携模式：exe 同目录有 portable.txt → 数据跟着 exe 走
    /// 2. 已有数据：exe 同目录已经有 history.json / settings.json → 继续用同目录（老用户不丢历史）
    /// 3. 其余情况（正规安装）：%APPDATA%\ClipPeek，卸载不会连历史一起删
    /// </summary>
    internal static class Paths
    {
        private const string PortableMarker = "portable.txt";

        private static string _dataDir;

        internal static string ExeDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        internal static string DataDirectory
        {
            get
            {
                if (_dataDir != null) return _dataDir;
                _dataDir = Resolve();
                return _dataDir;
            }
        }

        private static string Resolve()
        {
            try
            {
                string exeDir = ExeDirectory;

                if (File.Exists(Path.Combine(exeDir, PortableMarker))) return exeDir;
                if (File.Exists(Path.Combine(exeDir, "history.json"))) return exeDir;
                if (File.Exists(Path.Combine(exeDir, "settings.json"))) return exeDir;
            }
            catch
            {
            }

            try
            {
                string roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                Program.Trace("paths: roamingRoot='" + roamingRoot + "'");
                string roaming = Path.Combine(roamingRoot, "ClipPeek");
                Directory.CreateDirectory(roaming);
                return roaming;
            }
            catch (Exception ex)
            {
                Program.Trace("paths: fallback to exe dir, reason=" + ex.GetType().Name + ": " + ex.Message);
                return ExeDirectory;
            }
        }
    }
}
