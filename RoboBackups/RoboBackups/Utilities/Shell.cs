using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RoboBackups.Utilities
{
    static class Shell
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "rc")]
        public static void OpenUrl(IntPtr owner, Uri url)
        {
            Uri baseUri = new Uri(StartupPath);
            Uri resolved = new Uri(baseUri, url);

            // todo: support showing embedded pack:// resources in a popup page (could be useful for help content).
            const int SW_SHOWNORMAL = 1;
            int rc = ShellExecute(owner, "open", resolved.AbsoluteUri, null, StartupPath, SW_SHOWNORMAL);
        }

        public static string StartupPath
        {
            get
            {
                Process p = Process.GetCurrentProcess();
                string exe = p.MainModule.FileName;
                return Path.GetDirectoryName(exe);
            }
        }

        [DllImport("Shell32.dll", EntryPoint = "ShellExecuteA",
            SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int ShellExecute(IntPtr handle, string verb, string file,
            string args, string dir, int show);

    }
}
