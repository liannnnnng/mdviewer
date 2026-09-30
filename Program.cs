using System;
using System.Windows.Forms;

namespace MarkdownViewer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Handle file-association registration flags (require admin)
            if (args.Length > 0)
            {
                string verb = args[0].ToLowerInvariant().TrimStart('-', '/');

                if (verb == "register" || verb == "unregister")
                {
                    // If not elevated, re-launch with UAC elevation
                    if (!FileAssoc.IsElevated())
                    {
                        bool ok = FileAssoc.ElevateAndRerun(args);
                        string action = verb == "register" ? "注册" : "取消注册";
                        if (ok)
                            MessageBox.Show("正在请求管理员权限以" + action + "文件关联…",
                                "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show("需要管理员权限才能" + action + "文件关联。\n请右键以管理员身份运行。",
                                "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        if (verb == "register")
                        {
                            FileAssoc.Register();
                            MessageBox.Show("文件关联注册成功！\n\n.md 文件现在将使用 Markdown 查看器打开，并显示自定义图标。",
                                "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            FileAssoc.Unregister();
                            MessageBox.Show("文件关联已取消注册。\n\n.md 文件将恢复为系统默认打开方式。",
                                "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("操作失败：" + ex.Message,
                            "Markdown 查看器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
            }

            // Normal application startup
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
