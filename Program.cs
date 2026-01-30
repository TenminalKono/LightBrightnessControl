namespace LightBrightnessControl;

/// <summary>
/// 程序入口点
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序入口
    /// </summary>
    [STAThread]
    static void Main()
    {
        // 确保单实例运行
        using var mutex = new Mutex(true, "LightBrightnessControl_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // 已有实例运行，退出
            return;
        }

        // 启用视觉样式（使用系统原生控件外观）
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 处理未捕获的异常
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) =>
        {
            // 静默处理UI线程异常，避免程序崩溃
            System.Diagnostics.Debug.WriteLine($"UI Thread Exception: {e.Exception}");
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            // 记录非UI线程异常
            System.Diagnostics.Debug.WriteLine($"Unhandled Exception: {e.ExceptionObject}");
        };

        // 运行主窗体
        Application.Run(new MainForm());
    }
}
