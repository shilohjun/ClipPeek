using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ClipPeek
{
    internal static class Program
    {
        private static Mutex _instanceMutex;

        internal static void Trace(string message)
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("CLIPPEEK_TRACE");
                if (string.IsNullOrEmpty(path)) return;
                File.AppendAllText(path,
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        [STAThread]
        internal static int Main(string[] args)
        {
            Trace("main start");
            bool selfTest = HasFlag(args, "--selftest");
            bool quit = HasFlag(args, "--quit");
            bool preview = HasFlag(args, "--preview");
            Trace("flags selftest=" + selfTest + " quit=" + quit + " preview=" + preview);

            string exeDirectory = Paths.ExeDirectory;
            string dataDirectory = Paths.DataDirectory;
            Log.Init(dataDirectory);
            Trace("data dir=" + dataDirectory);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                Log.Error("ThreadException", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Error("UnhandledException", e.ExceptionObject as Exception);
            };

            if (quit && !selfTest)
            {
                SignalQuit();
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Ui.Init();
            Trace("ui scale=" + Ui.Scale);

            if (preview)
            {
                string output = Path.Combine(exeDirectory, "_preview");
                string temp = Path.Combine(Path.GetTempPath(), "clippeek-preview");
                try { Directory.CreateDirectory(temp); } catch { }
                ClipPeekContext pv = new ClipPeekContext(temp);
                pv.Preview(output);
                pv.Shutdown();
                Trace("preview finished");
                return 0;
            }

            if (selfTest)
            {
                Trace("selftest: creating context");
                string temp = Path.Combine(exeDirectory, "_selftest");
                try
                {
                    if (Directory.Exists(temp)) Directory.Delete(temp, true);
                    Directory.CreateDirectory(temp);
                }
                catch (Exception ex)
                {
                    Trace("selftest: dir failed " + ex.Message);
                }
                Trace("selftest: dir=" + temp + " exists=" + Directory.Exists(temp));
                ClipPeekContext probe = new ClipPeekContext(temp);
                Trace("selftest: context created");
                int code = probe.SelfTest(Path.Combine(exeDirectory, "selftest-result.txt"));
                Trace("selftest: finished code=" + code);
                probe.Shutdown();
                return code;
            }

            bool created;
            _instanceMutex = new Mutex(true, "ClipPeek.SingleInstance", out created);
            if (!created)
            {
                Log.Info("another instance is already running");
                return 0;
            }

            ClipPeekContext context = new ClipPeekContext(dataDirectory);
            context.Start();
            Application.Run(context);

            GC.KeepAlive(_instanceMutex);
            return 0;
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void SignalQuit()
        {
            try
            {
                EventWaitHandle handle = EventWaitHandle.OpenExisting("ClipPeek.Quit");
                handle.Set();
                handle.Close();
            }
            catch
            {
            }
        }
    }
}
