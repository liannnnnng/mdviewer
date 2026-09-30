using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownViewer
{
    public class MainForm : Form
    {
        private WebView2 web;
        private readonly string[] _args;
        private string _currentPath; // 当前打开文件完整路径；有则原地保存

        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int HTCAPTION = 2;
        private const int HTBOTTOMRIGHT = 17;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }

        private const int BorderThickness = 1;

        private Color BorderColor
        {
            get
            {
                bool active = (Form.ActiveForm == this);
                bool dark = IsDarkMode();
                if (active)
                    return dark ? Color.FromArgb(255, 120, 120, 120) : Color.FromArgb(255, 100, 100, 100);
                return dark ? Color.FromArgb(255, 60, 60, 60) : Color.FromArgb(255, 190, 190, 190);
            }
        }

        private void ApplyBorder()
        {
            try
            {
                BackColor = BorderColor;
                if (web != null)
                {
                    int m = BorderThickness;
                    web.SetBounds(m, m, ClientSize.Width - 2 * m, ClientSize.Height - 2 * m);
                }
            }
            catch { }
        }

        public MainForm(string[] args)
        {
            _args = args ?? Array.Empty<string>();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1200, 860);
            MinimumSize = new Size(560, 400);
            MaximizeBox = false;
            BackColor = Color.White;
            Text = "Markdown 查看器";

            web = new WebView2 { Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(web);

            ApplyBorder();
            Activated += (s, e) => ApplyBorder();
            Deactivate += (s, e) => ApplyBorder();
            Resize += (s, e) => ApplyBorder();
            Load += async (s, e) => await InitAsync();
            Load += (s, e) => ApplyBorder();
        }

        private static bool IsDarkMode()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null && key.GetValue("AppsUseLightTheme") is int v)
                        return v == 0;
                }
            }
            catch { }
            return false;
        }

        private async Task InitAsync()
        {
            try
            {
                string exeDir = AppContext.BaseDirectory;
                string www = Path.Combine(exeDir, "wwwroot");
                string udf = Path.Combine(Path.GetTempPath(), "MarkdownViewerWV2");
                Directory.CreateDirectory(udf);

                var env = await CoreWebView2Environment.CreateAsync(null, udf, null);
                await web.EnsureCoreWebView2Async(env);

                var cw = web.CoreWebView2;
                cw.SetVirtualHostNameToFolderMapping("app", www, CoreWebView2HostResourceAccessKind.Allow);
                cw.Settings.AreDevToolsEnabled = false;
                cw.Settings.AreDefaultContextMenusEnabled = false;
                cw.Settings.IsStatusBarEnabled = false;
                cw.WebMessageReceived += OnWebMessage;

                bool fired = false;
                cw.NavigationCompleted += (s2, e2) => { if (e2.IsSuccess && !fired) { fired = true; SendStartup(); } };
                cw.Navigate("https://app/index.html");
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化 WebView2 失败：\n" + ex.Message +
                    "\n\n请确认已安装 Microsoft Edge WebView2 Runtime。",
                    "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string cmd = null, name = null, text = null;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("cmd", out var c)) cmd = c.GetString();
                if (root.TryGetProperty("name", out var n)) name = n.GetString();
                if (root.TryGetProperty("text", out var t)) text = t.GetString();
            }
            catch { return; }

            switch (cmd)
            {
                case "drag": DoNative(HTCAPTION); break;
                case "resize": DoNative(HTBOTTOMRIGHT); break;
                case "min": WindowState = FormWindowState.Minimized; break;
                case "max": ToggleMax(); break;
                case "close": Close(); break;
                case "open": DoOpen(); break;
                case "save": DoSave(name, text); break;
                case "saveas": DoSaveAs(name, text); break;
            }
        }

        private void DoNative(int hit)
        {
            try { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero); } catch { }
        }

        private void ToggleMax()
        {
            WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        private void DoOpen()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "打开 Markdown 文件",
                Filter = "Markdown (*.md;*.markdown;*.mdown)|*.md;*.markdown;*.mdown|文本 (*.txt)|*.txt|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try { _currentPath = ofd.FileName; Deliver(Path.GetFileName(ofd.FileName), ReadAny(ofd.FileName)); }
                catch (Exception ex) { MessageBox.Show(this, "读取失败：" + ex.Message); }
            }
        }

        private void DoSave(string name, string text)
        {
            if (text == null) return;
            if (!string.IsNullOrEmpty(_currentPath))
            {
                try
                {
                    File.WriteAllText(_currentPath, text, new UTF8Encoding(false));
                    NotifySaved(Path.GetFileName(_currentPath));
                }
                catch (Exception ex) { MessageBox.Show(this, "保存失败：" + ex.Message); }
                return;
            }
            DoSaveAs(name, text);
        }

        private void DoSaveAs(string name, string text)
        {
            if (text == null) return;
            if (string.IsNullOrWhiteSpace(name)) name = "document.md";
            if (!HasExt(name)) name += ".md";
            using var sfd = new SaveFileDialog
            {
                Title = "另存为",
                Filter = "Markdown (*.md)|*.md|文本 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = name
            };
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(sfd.FileName, text, new UTF8Encoding(false));
                    _currentPath = sfd.FileName;
                    NotifySaved(Path.GetFileName(sfd.FileName));
                }
                catch (Exception ex) { MessageBox.Show(this, "保存失败：" + ex.Message); }
            }
        }

        private void NotifySaved(string fileName)
        {
            try { web.CoreWebView2.ExecuteScriptAsync("window.MDV && MDV.onSaved(" + JsonSerializer.Serialize(fileName) + ");"); } catch { }
        }

        private static bool HasExt(string n) =>
            n.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".mdown", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

        private void SendStartup()
        {
            string file = _args.FirstOrDefault(a => { try { return File.Exists(a); } catch { return false; } });
            if (file != null) { try { _currentPath = file; Deliver(Path.GetFileName(file), ReadAny(file)); } catch { } }
        }

        private void Deliver(string name, string text)
        {
            try
            {
                string js = "window.MDV && MDV.onFile(" + JsonSerializer.Serialize(name) + "," + JsonSerializer.Serialize(text) + ");";
                web.CoreWebView2.ExecuteScriptAsync(js);
            }
            catch { }
        }

        private static string ReadAny(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try { return new UTF8Encoding(false, true).GetString(StripBom(bytes)); }
            catch (DecoderFallbackException) { return Encoding.Default.GetString(bytes); }
        }
        private static byte[] StripBom(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return b.Skip(3).ToArray();
            return b;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(m.LParam);
                var screen = Screen.FromHandle(Handle);
                var wa = screen.WorkingArea;
                mmi.ptMaxPosition.X = wa.Left - screen.Bounds.Left;
                mmi.ptMaxPosition.Y = wa.Top - screen.Bounds.Top;
                mmi.ptMaxSize.X = wa.Width;
                mmi.ptMaxSize.Y = wa.Height;
                Marshal.StructureToPtr(mmi, m.LParam, true);
            }
            base.WndProc(ref m);
        }
    }
}
