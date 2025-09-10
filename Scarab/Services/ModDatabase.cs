using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Scarab.Services;

public class ModDatabase : IModDatabase
{
    private const string MODLINKS_URI = "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml";
    private const string APILINKS_URI = "https://raw.githubusercontent.com/hk-modding/modlinks/main/ApiLinks.xml";
        
    private const string FALLBACK_MODLINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ModLinks.xml";
    private const string FALLBACK_APILINKS_URI = "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ApiLinks.xml";

    public (string Url, int Version, string SHA256) Api { get; }

    public IEnumerable<ModItem> Items => _items;

    private readonly List<ModItem> _items = new();
    private readonly Dictionary<string, string> _chineseNames;
    private readonly Dictionary<string, ModChineseInfo> _chineseInfos;

    public ModDatabase(IModSource mods, ModLinks ml, ApiLinks al, Dictionary<string, string> chineseNames, Dictionary<string, ModChineseInfo> chineseInfos)
    {
        _chineseNames = chineseNames;
        _chineseInfos = chineseInfos;

        foreach (var mod in ml.Manifests)
        {
            var tags = mod.Tags.Select(x => Enum.TryParse(x, out Tag tag) ? (Tag?) tag : null)
                          .OfType<Tag>()
                          .ToImmutableArray();

            var name = mod.Name;
            var displayName = _chineseNames.TryGetValue(name, out var cn) ? cn : name;

            // �ϳ�����
            string description = mod.Description;
            if (_chineseInfos.TryGetValue(name, out var info) && !string.IsNullOrWhiteSpace(info.Desc))
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? info.Desc
                    : $"{description}\n\n{info.Desc}";
            }

            var item = new ModItem
            (
                link: mod.Links.OSUrl,
                version: mod.Version.Value,
                name: name,
                shasum: mod.Links.SHA256,
                description: description, // ֱ�Ӵ��ݺϳɺ������
                repository: mod.Repository,
                dependencies: mod.Dependencies,
                tags: tags,
                integrations: mod.Integrations,
                authors: mod.Authors,
                displayName: displayName,
                state: mods.FromManifest(mod)
            );
            _items.Add(item);
        }

        _items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        Api = (al.Manifest.Links.OSUrl, al.Manifest.Version, al.Manifest.Links.SHA256);
    }

    public ModDatabase(IModSource mods, (ModLinks ml, ApiLinks al) links) 
        : this(mods, links.ml, links.al, new Dictionary<string, string>(), new Dictionary<string, ModChineseInfo>()) { }

    public ModDatabase(IModSource mods, string modlinks, string apilinks) 
        : this(mods, FromString<ModLinks>(modlinks), FromString<ApiLinks>(apilinks), new Dictionary<string, string>(), new Dictionary<string, ModChineseInfo>()) { }
        
    public static async Task<(ModLinks, ApiLinks)> FetchContent(HttpClient hc)
    {
        var ml = FetchModLinks(hc);
        var al = FetchApiLinks(hc);

        await Task.WhenAll(ml, al);

        return (await ml, await al);
    }
        
    private static T FromString<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
            
        using TextReader reader = new StringReader(xml);

        var obj = (T?) serializer.Deserialize(reader);

        if (obj is null)
            throw new InvalidDataException();

        return obj;
    }

    private static async Task<ApiLinks> FetchApiLinks(HttpClient hc)
    {
        return FromString<ApiLinks>(await FetchWithFallback(hc, new Uri(APILINKS_URI), new Uri(FALLBACK_APILINKS_URI)));
    }
        
    private static async Task<ModLinks> FetchModLinks(HttpClient hc)
    {
        return FromString<ModLinks>(await FetchWithFallback(hc, new Uri(MODLINKS_URI), new Uri(FALLBACK_MODLINKS_URI)));
    }

    private static async Task<string> FetchWithFallback(HttpClient hc, Uri uri, Uri fallback)
    {
        try
        {
            var cts = new CancellationTokenSource(3000);
            return await hc.GetStringAsync(uri, cts.Token);
        }
        catch (Exception e) when (e is TaskCanceledException or HttpRequestException)
        {
            var cts = new CancellationTokenSource(3000);
            return await hc.GetStringAsync(fallback, cts.Token);
        }
    }

    public static async Task<Dictionary<string, ModChineseInfo>> FetchChineseNamesAsync(HttpClient hc)
    {
        var url = "https://ppcdn.dxinzf.com/ppstatic/tool/HollowKnight/HKModChineseName.json";
        using var response = await hc.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var encoding = response.Content.Headers.ContentEncoding;
        Stream stream = await response.Content.ReadAsStreamAsync();
        string json;
        if (encoding.Contains("gzip"))
        {
            using var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
            json = await reader.ReadToEndAsync();
        }
        else
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            json = await reader.ReadToEndAsync();
        }

        return JsonSerializer.Deserialize<Dictionary<string, ModChineseInfo>>(json) ?? new();
    }

    public static async Task<ModDatabase> CreateInstance(IModSource modSource)
    {
        using var hc = new HttpClient();
        var chineseInfos = await FetchChineseNamesAsync(hc);
        var chineseNames = chineseInfos.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ChineseName
        );
        var (modLinks, apiLinks) = await FetchContent(hc);
        return new ModDatabase(modSource, modLinks, apiLinks, chineseNames, chineseInfos);
    }

    public class ModChineseInfo
    {
        public string ChineseName { get; set; } = "";
        public string Desc { get; set; } = "";
    }

    public string ModDescription
    {
        get
        {
            if (SelectedMod == null) return string.Empty;
            var en = SelectedMod.Description;
            var cn = _chineseInfos.TryGetValue(SelectedMod.Name, out var info) ? info.Desc : "";
            return string.IsNullOrWhiteSpace(cn) ? en : $"{en}\n{cn}";
        }
    }

    // �� ModDatabase ������� SelectedMod ����
    public ModItem? SelectedMod { get; set; }
}