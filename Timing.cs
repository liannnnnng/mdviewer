using System;
using System.IO;

namespace MarkdownViewer
{
    /// <summary>
    /// 启动时间线打点：设置环境变量 MDV_TIMING=1 时，把各里程碑耗时追加到
    /// %TEMP%\mdv-timing.log（毫秒，相对本进程启动）。用于诊断真实冷启动。
    /// </summary>
    static class Timing
    {
        static readonly int _t0 = Environment.TickCount;

        public static void Log(string what)
        {
            try
            {
                if (Environment.GetEnvironmentVariable("MDV_TIMING") != "1") return;
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "mdv-timing.log"),
                    (Environment.TickCount - _t0) + "ms\t" + what + "\r\n");
            }
            catch { }
        }
    }
}
