using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace MarkdownViewer
{
    /// <summary>
    /// Handles .md file association registration (ProgID + DefaultIcon + shell\open).
    /// Run with --register or --unregister; requires admin privileges.
    /// </summary>
    static class FileAssoc
    {
        private const string ProgID = "MarkdownViewer.md";
        private static readonly string[] Extensions = { ".md", ".markdown", ".mdown" };

        /// <summary>
        /// Returns true when the current process is elevated.
        /// </summary>
        public static bool IsElevated()
        {
            using var id = WindowsIdentity.GetCurrent();
            var pr = new WindowsPrincipal(id);
            return pr.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Re-launch the current executable with verb "runas" (UAC elevation).
        /// Returns true if the elevated process was started.
        /// </summary>
        public static bool ElevateAndRerun(string[] extraArgs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Verb = "runas",
                    UseShellExecute = true,
                    Arguments = string.Join(" ", extraArgs)
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                // User declined UAC or other error
                return false;
            }
        }

        /// <summary>
        /// Register .md / .markdown / .mdown → ProgID → exe + icon.
        /// Must run as admin.
        /// </summary>
        public static void Register()
        {
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string icoPath = Path.Combine(Path.GetDirectoryName(exePath), "md.ico");
            string iconValue = File.Exists(icoPath) ? icoPath + ",0" : exePath + ",0";

            // ProgID: MarkdownViewer.md
            using (var pid = Registry.ClassesRoot.CreateSubKey(ProgID))
            {
                pid.SetValue("", "Markdown 文件");
                pid.SetValue("FriendlyTypeName", "Markdown 文件");
                pid.SetValue("DefaultIcon", iconValue);
            }

            // shell\open\command
            using (var cmd = Registry.ClassesRoot.CreateSubKey(ProgID + @"\shell\open\command"))
            {
                cmd.SetValue("", "\"" + exePath + "\" \"%1\"");
            }

            // Associate each extension with the ProgID
            foreach (string ext in Extensions)
            {
                using (var ek = Registry.ClassesRoot.CreateSubKey(ext))
                {
                    // Backup previous value
                    string prev = ek.GetValue("") as string;
                    if (!string.IsNullOrEmpty(prev) && prev != ProgID)
                        ek.SetValue("PreOpenWith", prev);

                    ek.SetValue("", ProgID);
                }
            }

            // Notify Explorer to refresh icons
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Remove the association: restore extensions and delete ProgID.
        /// Must run as admin.
        /// </summary>
        public static void Unregister()
        {
            foreach (string ext in Extensions)
            {
                try
                {
                    using (var ek = Registry.ClassesRoot.OpenSubKey(ext, true))
                    {
                        if (ek == null) continue;
                        string current = ek.GetValue("") as string;
                        if (current == ProgID)
                        {
                            string prev = ek.GetValue("PreOpenWith") as string;
                            if (!string.IsNullOrEmpty(prev))
                                ek.SetValue("", prev);
                            else
                                ek.DeleteValue("", false);
                            ek.DeleteValue("PreOpenWith", false);
                        }
                    }
                }
                catch { }
            }

            try { Registry.ClassesRoot.DeleteSubKeyTree(ProgID, false); } catch { }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }

        // Shell notifications
        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_IDLIST = 0x0000;

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
    }
}
