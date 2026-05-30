using System.Text.Json;

namespace GunAPP;

/// <summary>
/// 应用设置（持久化）
/// </summary>
internal static class AppSettings
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KuaiGunAPP", "settings.json");

    private static SettingsData _data = new();

    /// <summary>轮询间隔（毫秒）</summary>
    public static int PollingInterval
    {
        get => _data.PollingInterval;
        set
        {
            _data.PollingInterval = value;
            Save();
        }
    }

    /// <summary>响应速度名称</summary>
    public static string SpeedName => _data.PollingInterval switch
    {
        800 => "较慢",
        200 => "正常",
        50 => "快速",
        20 => "极速",
        _ => "自定义"
    };

    /// <summary>加载设置</summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch
        {
            _data = new SettingsData();
        }
    }

    /// <summary>保存设置</summary>
    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    /// <summary>切换到下一个速度</summary>
    public static void CycleSpeed()
    {
        PollingInterval = PollingInterval switch
        {
            800 => 200,
            200 => 50,
            50 => 20,
            20 => 800,
            _ => 200
        };
    }
}

internal class SettingsData
{
    public int PollingInterval { get; set; } = 200;
}
