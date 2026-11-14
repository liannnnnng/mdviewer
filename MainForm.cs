using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownViewer
{
    public class MainForm : Form
    {
        private WebView2 web;
        private Panel _placeholder;          // ① 主题色加载占位，盖住 WebView2 直到首帧内容就绪
        private bool _revealed;
        private static Task<CoreWebView2Environment> _envTask; // ② 预热：浏览器进程拉起越早越好
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
                    var x = m;
                    var y = m;
                    var w = ClientSize.Width - 2 * m;
                    var h = ClientSize.Height - 2 * m;
                    web.SetBounds(x, y, w, h);
                    if (_placeholder != null) _placeholder.SetBounds(x, y, w, h);
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

            // 任务栏 / Alt-Tab 图标：md.ico 同时也是文件关联（DefaultIcon）用的文件
            try
            {
                string icoPath = Path.Combine(AppContext.BaseDirectory, "md.ico");
                if (File.Exists(icoPath)) Icon = new Icon(icoPath);
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            web = new WebView2 { Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(web);

            // ① 占位层盖在 WebView2 上面：窗口先显示主题色加载画面，绝不露白板
            _placeholder = new PlaceholderPanel();
            _placeholder.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _placeholder.Paint += DrawPlaceholder;
            Controls.Add(_placeholder);
            _placeholder.BringToFront();

            ApplyBorder();
            Activated += (s, e) => ApplyBorder();
            Deactivate += (s, e) => ApplyBorder();
            Resize += (s, e) => ApplyBorder();
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

        /// <summary>
        /// 在 Application.Run 之前调用：提前创建窗口句柄并启动 WebView2 环境预热，
        /// 让 msedgewebview2 进程树的拉起与 WinForms 启动并行进行（②）。
        /// </summary>
        public void Start()
        {
            Timing.Log("handle");
            var h = Handle; // 提前创建句柄：占位层、兜底计时器与 WebView2 控件都依赖它
            ApplyBorder();
            Shown += (s, e) => Timing.Log("shown");
            _ = InitAsync();
        }

        /// <summary>环境只创建一次；首次调用即在后台开始拉起浏览器进程树。</summary>
        private static Task<CoreWebView2Environment> PrewarmAsync()
        {
            if (_envTask == null)
            {
                string udf = Path.Combine(Path.GetTempPath(), "MarkdownViewerWV2");
                Directory.CreateDirectory(udf);
                var opts = new CoreWebView2EnvironmentOptions();
                // 加载占位层会完全遮住 WebView2；不关掉原生遮挡计算，Chromium 会把
                // 被遮住的浏览器当后台页节流，导航要白等约 2 秒（实测 navcomplete 2.1s）。
                opts.AdditionalBrowserArguments = "--disable-features=CalculateNativeWinOcclusion";
                _envTask = CoreWebView2Environment.CreateAsync(null, udf, opts);
            }
            return _envTask;
        }

        private async Task InitAsync()
        {
            try
            {
                string www = Path.Combine(AppContext.BaseDirectory, "wwwroot");

                var env = await PrewarmAsync();
                Timing.Log("env-spawned");
                await web.EnsureCoreWebView2Async(env);
                Timing.Log("controller");

                var cw = web.CoreWebView2;
                cw.Settings.AreDevToolsEnabled = false;
                cw.Settings.AreDefaultContextMenusEnabled = false;
                cw.Settings.IsStatusBarEnabled = false;
                cw.WebMessageReceived += OnWebMessage;

                bool fired = false;
                cw.NavigationStarting += (s2, e2) => Timing.Log("navstarting");
                cw.NavigationCompleted += (s2, e2) =>
                {
                    if (e2.IsSuccess && !fired)
                    {
                        fired = true;
                        Timing.Log("navcomplete");
                        SendStartup();
                        Timing.Log("startup-sent");
                    }
                };
                // file:// 直连加载 wwwroot。不用 SetVirtualHostNameToFolderMapping 的
                // https://app 虚拟主机：实测其请求会被部分环境（网络过滤/代理驱动）拖慢约 2 秒，
                // 而 file:// 在任何机器上都是纯本地读取（脚本 ~120ms 即执行完）。
                // 本应用无 fetch/XHR/localStorage 使用，file 源无功能影响。
                string url = new Uri(Path.Combine(www, "index.html")).AbsoluteUri;
                cw.Navigate(url);
                Timing.Log("navigate");

                // 兜底：JS 侧 ready 迟迟不来（渲染器异常等）也要撤掉占位
                var safety = new System.Windows.Forms.Timer { Interval = 3000 };
                safety.Tick += (s, e) => { RevealWeb(); safety.Stop(); };
                safety.Start();
            }
            catch (Exception ex)
            {
                RevealWeb();
                MessageBox.Show(this, "初始化 WebView2 失败：\n" + ex.Message +
                    "\n\n请确认已安装 Microsoft Edge WebView2 Runtime。",
                    "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string cmd, name, text;
            try
            {
                if (!JsonLite.TryParseMessage(e.WebMessageAsJson, out cmd, out name, out text)) return;
            }
            catch { return; }

            switch (cmd)
            {
                case "ready": RevealWeb(); break;
                case "js-timing": Timing.Log("js-scripts-done t=" + name); break;
                case "js-load": Timing.Log("js-load-event t=" + name); break;
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
            try { web.CoreWebView2.ExecuteScriptAsync("window.MDV && MDV.onSaved(" + JsonLite.Escape(fileName) + ");"); } catch { }
        }

        private static bool HasExt(string n) =>
            n.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".mdown", StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

        /// <summary>① 内容首帧就绪：撤掉主题色占位，露出下面已经画好的 WebView2。</summary>
        private void RevealWeb()
        {
            if (_revealed) return;
            _revealed = true;
            Timing.Log("reveal");
            try { if (_placeholder != null) _placeholder.Visible = false; } catch { }
        }

        private void SendStartup()
        {
            string file = _args.FirstOrDefault(a => { try { return File.Exists(a); } catch { return false; } });
            if (file != null)
            {
                try { _currentPath = file; Deliver(Path.GetFileName(file), ReadAny(file)); } catch { }
            }
            else
            {
                // 无启动文件：让 JS 侧上报 ready（拖放提示界面就是最终状态）
                try { web.CoreWebView2.ExecuteScriptAsync("window.MDV && MDV.noFile && MDV.noFile();"); } catch { }
            }
        }

        private void DrawPlaceholder(object sender, PaintEventArgs e)
        {
            bool dark = IsDarkMode();
            Color bg = dark ? Color.FromArgb(0x16, 0x1b, 0x22) : Color.FromArgb(0xf6, 0xf8, 0xfa);
            Color fg = dark ? Color.FromArgb(0x8d, 0x96, 0xa0) : Color.FromArgb(0x65, 0x6d, 0x76);
            var g = e.Graphics;
            g.Clear(bg);

            int w = _placeholder.ClientSize.Width;
            int h = _placeholder.ClientSize.Height;

            try
            {
                string png = Path.Combine(AppContext.BaseDirectory, "md.png");
                if (File.Exists(png))
                {
                    using (var img = Image.FromFile(png))
                    {
                        int s = 72;
                        g.DrawImage(img, w / 2 - s / 2, h / 2 - s / 2 - 44, s, s);
                    }
                }
            }
            catch { }

            const string text = "正在加载…";
            using (var f = new Font("Segoe UI", 11f))
            using (var b = new SolidBrush(fg))
            {
                var sz = g.MeasureString(text, f);
                g.DrawString(text, f, b, w / 2 - sz.Width / 2, h / 2 + 44);
            }
        }

        /// <summary>双缓冲占位面板，避免加载画面闪烁。</summary>
        private class PlaceholderPanel : Panel
        {
            public PlaceholderPanel() { DoubleBuffered = true; }
        }

        private void Deliver(string name, string text)
        {
            try
            {
                string js = "window.MDV && MDV.onFile(" + JsonLite.Escape(name) + "," + JsonLite.Escape(text) + ");";
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
