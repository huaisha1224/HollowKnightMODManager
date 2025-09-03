using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Diagnostics; // 添加调试输出

namespace Scarab.Mock;

public class MockDatabase : IModDatabase, INotifyPropertyChanged
{
    IEnumerable<ModItem> IModDatabase.Items => Items;
    public ObservableCollection<ModItem> Items { get; } = new();

    public (string Url, int Version, string SHA256) Api { get; } = ("...", 256, "?");

    public event PropertyChangedEventHandler? PropertyChanged;

    public MockDatabase()
    {
        System.Diagnostics.Debug.WriteLine("[调试] MockDatabase 构造函数被调用");
        _ = LoadChineseNamesAndFillAsync();
    }

    private async Task LoadChineseNamesAndFillAsync()
    {
        var cnDict = await LoadChineseNamesAsync();
        // 调试输出：打印所有 key 和 value
        //Debug.WriteLine($"[调试] 中文名字典条数: {cnDict.Count}");
        foreach (var kv in cnDict)
        //{
        //    Debug.WriteLine($"[调试] key: {kv.Key}, value: {kv.Value}");
        //}
        // 检查特定key
        //Debug.WriteLine($"[调试] NormalEx 映射: {(cnDict.TryGetValue("NormalEx", out var normalEx) ? normalEx : "无")}");
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Items.Clear();
            Items.Add(new ModItem(
                new InstalledState(true, new Version(1, 0), true),
                new Version(1, 0),
                new[] { "ILove", "Having", "Dependencies" },
                "link",
                "sha",
                "NormalEx",
                "An example",
                "https://github.com/fifty-six/HollowKnight.QoL",
                ImmutableArray.Create(Tag.Boss, Tag.Utility),
                new[] { "ILove", "Having", "Integrations" },
                new[] { "56", "57", "58" },
                GetChineseName("NormalEx", cnDict)
            ));
            Items.Add(new ModItem(
                new InstalledState(true, new Version(1, 0), false),
                new Version(2, 0),
                Array.Empty<string>(),
                "link",
                "sha",
                "OutOfDateEx",
                "An example",
                "https://github.com/fifty-six/yup",
                ImmutableArray.Create(Tag.Library),
                Array.Empty<string>(),
                Array.Empty<string>(),
                GetChineseName("OutOfDateEx", cnDict)
            ));
            Items.Add(new ModItem(
                new NotInstalledState(),
                new Version(1, 0),
                Array.Empty<string>(),
                "link",
                "sha",
                "NotInstalledEx",
                "An example",
                "example.com",
                ImmutableArray<Tag>.Empty,
                Array.Empty<string>(),
                Array.Empty<string>(),
                GetChineseName("NotInstalledEx", cnDict)
            ));
            Items.Add(new ModItem(
                new NotInstalledState(),
                new Version(1, 0),
                Array.Empty<string>(),
                "link",
                "sha",
                string.Join("", Enumerable.Repeat("Very", 8)) + "LongModName",
                "An example",
                "https://example.com/really/really/really/really/really/long/url/to/test/wrapping/impls/....",
                ImmutableArray.Create(Tag.Cosmetic, Tag.Expansion, Tag.Gameplay),
                new[] { "NormalEx" },
                Array.Empty<string>(),
                GetChineseName(string.Join("", Enumerable.Repeat("Very", 8)) + "LongModName", cnDict)
            ));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
        });
    }

    // 加载中文名的异步方法
    private static async Task<Dictionary<string, string>> LoadChineseNamesAsync()
    {
        try
        {
            using var client = new HttpClient();
            var json = await client.GetStringAsync("https://ppcdn.dxinzf.com/ppstatic/tool/HollowKnight/HKChineseName.json");
            //Debug.WriteLine($"[调试] 获取到的JSON内容: {json}");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"[调试] 加载中文名异常: {ex}");
            return new();
        }
    }

    private static string GetChineseName(string name, Dictionary<string, string>? dict)
    {
        if (dict != null && dict.TryGetValue(name, out var cn) && !string.IsNullOrWhiteSpace(cn))
            return cn;
        return name;
    }
}