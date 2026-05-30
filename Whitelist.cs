using System.Text.Json;

namespace GunAPP;

/// <summary>
/// 白名单管理：持久化存储，程序重启后保留
/// </summary>
internal static class Whitelist
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KuaiGunAPP", "whitelist.json");

    private static HashSet<string> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>加载白名单</summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<WhitelistEntry>>(json);
                if (list != null)
                {
                    _entries = new HashSet<string>(
                        list.Select(e => e.ExePath),
                        StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch { }
    }

    /// <summary>保存白名单</summary>
    public static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var list = _entries.Select(e => new WhitelistEntry
            {
                ExePath = e,
                AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ProgramName = Path.GetFileNameWithoutExtension(e),
            }).ToList();

            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    /// <summary>是否在白名单中</summary>
    public static bool Contains(string exePath) => _entries.Contains(exePath);

    /// <summary>添加到白名单</summary>
    public static void Add(string exePath)
    {
        if (_entries.Add(exePath)) Save();
    }

    /// <summary>从白名单移除</summary>
    public static void Remove(string exePath)
    {
        if (_entries.Remove(exePath)) Save();
    }

    /// <summary>获取所有白名单条目</summary>
    public static List<string> GetAll() => _entries.ToList();

    /// <summary>清空白名单</summary>
    public static void Clear()
    {
        _entries.Clear();
        Save();
    }
}

internal class WhitelistEntry
{
    public string ExePath { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public string AddedAt { get; set; } = "";
}
