using System.Diagnostics;
using System.IO;

namespace GunAPP;

internal static class Program
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KuaiGunAPP");
    private static readonly string FirstRunFile = Path.Combine(ConfigDir, ".initialized");

    [STAThread]
    static void Main(string[] args)
    {
        // 单实例检查
        using var mutex = new Mutex(true, @"Global\KuaiGunAPP_SingleInstance", out bool isNew);
        if (!isNew)
        {
            if (!args.Contains("--silent"))
                NotifyForm.Show("快滚!APP!", "快滚!APP! 已在运行中。\n\n请查看系统托盘。");
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // 加载白名单
        Whitelist.Load();

        // 加载设置
        AppSettings.Load();

        // 预加载赞赏码（异步）
        DonateImageCache.Preload();

        bool isSilent = args.Contains("--silent");

        // 检查是否首次运行
        bool isFirstRun = !File.Exists(FirstRunFile);
        if (isFirstRun)
        {
            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(FirstRunFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            // 首次运行：同步加载赞赏码
            var donateImg = DonateImageCache.Image ?? DonateImageCache.LoadSync();

            NotifyForm.ShowWithDonate("快滚!APP!",
                "快滚!APP! 已安装完成！\n\n" +
                "程序将在后台运行，监控回收站。\n" +
                "右键托盘图标可管理设置。",
                donateImg);
        }
        else if (!isSilent)
        {
            NotifyForm.Show("快滚!APP!", "快滚!APP! 已开启。\n\n请查看系统托盘。");
        }

        Application.Run(new MainForm());
    }
}
