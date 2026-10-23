using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ClipPeek
{
    internal static class Autostart
    {
        internal static string ShortcutPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    "ClipPeek.lnk");
            }
        }

        internal static bool IsEnabled()
        {
            try
            {
                return File.Exists(ShortcutPath);
            }
            catch
            {
                return false;
            }
        }

        internal static bool Enable()
        {
            try
            {
                string exe = Application.ExecutablePath;
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return false;

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember(
                    "CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                    new object[] { ShortcutPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Path.GetDirectoryName(exe) });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "ClipPeek 剪贴板气泡与历史" });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { exe + ",0" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                return File.Exists(ShortcutPath);
            }
            catch (Exception ex)
            {
                Log.Error("Autostart.Enable", ex);
                return false;
            }
        }

        internal static bool Disable()
        {
            try
            {
                if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath);
                return !File.Exists(ShortcutPath);
            }
            catch (Exception ex)
            {
                Log.Error("Autostart.Disable", ex);
                return false;
            }
        }
    }
}
