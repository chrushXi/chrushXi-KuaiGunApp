namespace GunAPP;

/// <summary>
/// 隐藏的消息窗口 + 系统托盘
/// </summary>
internal sealed class MainForm : Form
{
    public TrayIcon TrayIcon => _trayIcon;
    private readonly TrayIcon _trayIcon;

    public MainForm()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-10000, -10000);
        Size = new Size(1, 1);

        _trayIcon = new TrayIcon();
        _trayIcon.OnExit += () => Close();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var _ = Handle;
        ShellMonitor.Start(Handle, OnShortcutToRecycleBin);
    }

    private void OnShortcutToRecycleBin(string recyclePath, string originalName)
    {
        // 1. 解析快捷方式
        var lnkInfo = LnkParser.Resolve(recyclePath);
        if (!lnkInfo.Valid) return;

        // 2. 检查白名单
        if (Whitelist.Contains(lnkInfo.TargetPath)) return;

        // 3. 查找卸载信息
        var uninstallInfo = UninstallFinder.Find(lnkInfo.TargetPath);

        if (!uninstallInfo.Found)
        {
            // 没找到卸载程序，提示用户是否直接删除文件夹
            var programName = Path.GetFileNameWithoutExtension(lnkInfo.TargetPath);
            var dir = Path.GetDirectoryName(lnkInfo.TargetPath);

            var deleteChoice = NotifyForm.ShowConfirm(
                "快滚!APP!",
                $"未找到 [{programName}] 的卸载程序。\n\n" +
                $"程序位置：{lnkInfo.TargetPath}\n\n" +
                "是否直接删除程序文件夹？",
                "删除文件夹", "取消");

            if (deleteChoice && !string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                try
                {
                    Directory.Delete(dir, true);
                    NotifyForm.Show("快滚!APP!", $"已删除 {dir}");
                }
                catch (Exception ex)
                {
                    NotifyForm.Show("快滚!APP!", $"删除失败：{ex.Message}");
                }
            }

            MemoryTrim.Trim();
            return;
        }

        // 4. 弹窗确认卸载
        var displayName = string.IsNullOrEmpty(uninstallInfo.DisplayName)
            ? Path.GetFileNameWithoutExtension(lnkInfo.TargetPath)
            : uninstallInfo.DisplayName;

        var choice = ConfirmDialog.Show(displayName, uninstallInfo.DisplayIcon);

        // 弹窗关闭后修剪内存
        MemoryTrim.Trim();

        // 5. 执行
        switch (choice)
        {
            case ConfirmDialog.UserChoice.Uninstall:
                UninstallExecutor.Execute(uninstallInfo.UninstallString);
                break;
            case ConfirmDialog.UserChoice.WhitelistUninstall:
                Whitelist.Add(lnkInfo.TargetPath);
                UninstallExecutor.Execute(uninstallInfo.UninstallString);
                break;
            case ConfirmDialog.UserChoice.WhitelistDelete:
                Whitelist.Add(lnkInfo.TargetPath);
                break;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ShellMonitor.Shutdown();
        _trayIcon.Dispose();
        base.OnFormClosing(e);
    }
}
