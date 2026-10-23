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

        /// <summary>数据目录是否真的能写（有些环境会在 %APPDATA% 上拦写入）。</summary>
        internal static bool CanWrite(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write-probe");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                Program.Trace("paths: cannot write '" + directory + "': " + ex.GetType().Name);
                return false;
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

            string roaming = null;
            try
            {
                string roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                Program.Trace("paths: roamingRoot='" + roamingRoot + "'");
                roaming = Path.Combine(roamingRoot, "ClipPeek");
                if (CanWrite(roaming)) return roaming;
            }
            catch (Exception ex)
            {
                Program.Trace("paths: roaming failed, reason=" + ex.GetType().Name);
            }

            // %APPDATA% 写不进去时依次退到 LOCALAPPDATA、再退到 exe 同目录
            try
            {
                string localRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string local = Path.Combine(localRoot, "ClipPeek");
                if (CanWrite(local))
                {
                    Program.Trace("paths: falling back to local appdata=" + local);
                    return local;
                }
            }
            catch
            {
            }

            if (CanWrite(ExeDirectory))
            {
                Program.Trace("paths: falling back to exe dir=" + ExeDirectory);
                return ExeDirectory;
            }

            Program.Trace("paths: no writable dir, using roaming anyway=" + roaming);
            return roaming ?? ExeDirectory;
        }
    }
}
