using System.Diagnostics;
using System.IO.Abstractions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using MessageBox.Avalonia;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using Microsoft.Extensions.DependencyInjection;
using DryIoc; // 在文件顶部添加这一行

namespace Scarab.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();

    private readonly ReactiveCommand<Unit, Unit> _initialization;
    
    private static bool _Debug
    {
        get {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
        
    [Notify]
    private ReactiveObject? _content;

    [Notify] 
    private SettingsViewModel? _settingsPage;

    private async Task Impl()
    {
        await CheckForUpdateAsync(); // 检查更新
        Log.Information("Checking if up to date...");
            
        await CheckUpToDate();

        var con = new Container();

        Log.Information("Loading settings.");
        Settings settings = Settings.Load() ?? Settings.Create(await GetSettingsPath());
        settings.Apply();

        if (!PathUtil.ValidateExisting(settings.ManagedFolder))
        {
            Log.Information("Settings path {Previous} is invalid, forcing re-selection.", settings.ManagedFolder);
            settings = await ResetSettings();
        }

        Log.Information("Fetching links");
            
        (ModLinks ml, ApiLinks al) content;

        void AddSettings(HttpClient hc)
        {
            hc.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                MustRevalidate = true
            };
                
            hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");
        }

        HttpClient hc;
            
        try
        {
            var res = await WorkaroundHttpClient.TryWithWorkaroundAsync(
                settings.RequiresWorkaroundClient 
                    ? WorkaroundHttpClient.Settings.OnlyWorkaround
                    : WorkaroundHttpClient.Settings.TryBoth,
                ModDatabase.FetchContent,
                AddSettings
            );

            content = res.Result;

            if (res.NeededWorkaround && !settings.RequiresWorkaroundClient)
            {
                settings.RequiresWorkaroundClient = true;
                settings.Save();
            }

            hc = res.Client;
        }
        catch (Exception e) when (e is TaskCanceledException { CancellationToken.IsCancellationRequested: true } or HttpRequestException)
        {
            string failedOp = e switch
            {
                TaskCanceledException => Resources.MWVM_Impl_Error_Fetch_ModLinks_Timeout,
                HttpRequestException http => string.Format(Resources.MWVM_Impl_Error_Fetch_ModLinks_Error, http.StatusCode),
                _ => throw new ArgumentOutOfRangeException()
            };
                
            await MessageBoxManager.GetMessageBoxStandardWindow
            (
                title: Resources.MWVM_Impl_Error_Fetch_ModLinks_Msgbox_Title,
                text: string.Format(Resources.MWVM_Impl_Error_Fetch_ModLinks_Msgbox_Text, failedOp),
                icon: Icon.Error
            ).Show();

            throw;
        }

        Log.Information("Fetched links successfully");

        con.AddLogging();
        con.RegisterInstance(hc);
        con.RegisterDelegate<ISettings>(_ => settings);
        con.Register<IFileSystem, FileSystem>();
        
        var mods = await InstalledMods.Load(
            con.GetRequiredService<IFileSystem>(),
            settings,
            content.ml
        );
        var chineseNames = await ModDatabase.FetchChineseNamesAsync(hc);
        con.RegisterInstance<IModSource>(mods);

        con.RegisterDelegate<IModSource, IModDatabase>(src => new ModDatabase(src, content.ml, content.al, chineseNames));
        con.Register<IInstaller, Installer>();
        con.Register<ModPageViewModel>();
        con.Register<SettingsViewModel>();

        con.ValidateAndThrow();
        
        Logger.Sink = new MicrosoftLogSink(
            con.Resolve<ILoggerFactory>().CreateLogger("Avalonia"),
            LogArea.Platform,
            LogArea.macOSPlatform,
            LogArea.X11Platform,
            LogArea.Binding,
            LogArea.Animations,
            LogArea.Control,
            LogArea.Property,
            LogArea.Visual
        );
        
        Log.Information("Displaying model");
        
        SettingsPage = con.GetRequiredService<SettingsViewModel>();
        Content = con.GetRequiredService<ModPageViewModel>();
    }

    private static async Task CheckUpToDate()
    {
        Version? current_version = Assembly.GetExecutingAssembly().GetName().Version;
            
        Log.Information("Current version of installer is {Version}", current_version);

        if (_Debug)
            return; 

        const string gh_releases = "https://api.github.com/repos/fifty-six/Scarab/releases/latest";

        string json;
            
        try
        {
            var hc = new HttpClient();
                
            hc.DefaultRequestHeaders.Add("User-Agent", "Scarab");

            json = await hc.GetStringAsync(new Uri(gh_releases));
        }
        catch (Exception e) when (e is HttpRequestException or TimeoutException) {
            return;
        }

        JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("tag_name", out JsonElement tag_elem))
            return;

        string? tag = tag_elem.GetString();

        if (tag is null)
            return;

        if (tag.StartsWith("v"))
            tag = tag[1..];

        if (!Version.TryParse(tag.Length == 1 ? tag + ".0.0.0" : tag, out Version? version))
            return;

        if (version <= current_version)
            return;
            
        string? res = await MessageBoxManager.GetMessageBoxCustomWindow
        (
            new MessageBoxCustomParams {
                ButtonDefinitions = new[] {
                    new ButtonDefinition {
                        IsDefault = true,
                        IsCancel = true,
                        Name = Resources.MWVM_OutOfDate_GetLatest
                    },
                    new ButtonDefinition {
                        Name = Resources.MWVM_OutOfDate_ContinueAnyways
                    }
                },
                ContentTitle = Resources.MWVM_OutOfDate_Title,
                ContentMessage = Resources.MWVM_OutOfDate_Message,
                SizeToContent = SizeToContent.WidthAndHeight
            }
        ).Show();

        if (res == Resources.MWVM_OutOfDate_GetLatest)
        {
            Process.Start(new ProcessStartInfo("https://github.com/fifty-six/Scarab/releases/latest") { UseShellExecute = true });
                
            ((IClassicDesktopStyleApplicationLifetime?) Application.Current?.ApplicationLifetime)?.Shutdown();
        }
        else
        {
            Log.Warning(
                "Installer out of date! Version {Current_version} with latest {Version}!",
                current_version,
                version
            );
        }
    }
        
    private static async Task<Settings> ResetSettings()
    {
        await MessageBoxManager.GetMessageBoxStandardWindow
        (
            new MessageBoxStandardParams {
                ContentHeader = Resources.MWVM_Warning,
                ContentMessage = Resources.MWVM_InvalidSavedPath_Message,
                // The auto-resize for this lib is buggy, so 
                // ensure that the message doesn't get cut off 
                MinWidth = 550
            }
        ).Show();

        return Settings.Create(await GetSettingsPath());
    }

    private static async Task<string> GetSettingsPath()
    {
        if (!Settings.TryAutoDetect(out ValidPath? path))
        {
            Log.Information("Unable to detect installation path for settings, selecting manually.");
            
            IMsBoxWindow<ButtonResult> info = MessageBoxManager.GetMessageBoxStandardWindow
            (
                new MessageBoxStandardParams
                {
                    ContentHeader = Resources.MWVM_Info,
                    ContentMessage = Resources.MWVM_UnableToDetect_Message,
                    MinWidth = 550
                }
            );

            await info.Show();
                
            return await PathUtil.SelectPath();
        }

        Log.Information("Settings doesn't exist. Creating it at detected path {Path}.", path);

        IMsBoxWindow<ButtonResult> window = MessageBoxManager.GetMessageBoxStandardWindow
        (
            new MessageBoxStandardParams
            {
                ContentHeader = Resources.MWVM_DetectedPath_Title,
                ContentMessage = string.Format(Resources.MWVM_DetectedPath_Message, path.Root),
                ButtonDefinitions = ButtonEnum.YesNo
            }
        );

        ButtonResult res = await window.Show();

        return res == ButtonResult.Yes
            ? Path.Combine(path.Root, path.Suffix)
            : await PathUtil.SelectPath();
    }

    public MainWindowViewModel()
    {
        _initialization = ReactiveCommand.CreateFromTask(OnInitialized);

        this.WhenActivated(
            disposable => _initialization.Execute().Subscribe().DisposeWith(disposable)
        );
    }

    private async Task OnInitialized()
    {
        try
        {
            await Impl();
        }
        catch (Exception e)
        {
            Log.Fatal(e, "Fatal error in MainWindowViewModel startup!");

            if (Debugger.IsAttached)
                Debugger.Break();

            Environment.Exit(-1);

            throw;
        }
    }

    // 判断是否需要更新
    private async Task CheckForUpdateAsync()
    {
        try
        {
            using var hc = new HttpClient();
            var resp = await hc.GetStringAsync("https://api.smarttesting.cn/tools/HKModManager");   //版本检查API
            using var doc = JsonDocument.Parse(resp);
            var root = doc.RootElement;
            var remoteVersion = root.GetProperty("Version").GetString();
            var downloadUrl = root.GetProperty("DownloadUrl").GetString();

            var localVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

            if (!string.IsNullOrEmpty(remoteVersion) && !string.IsNullOrEmpty(localVersion)
                && new Version(remoteVersion) > new Version(localVersion))
            {
                var result = await MessageBoxManager.GetMessageBoxCustomWindow(
                    new MessageBoxCustomParams
                    {
                        ContentTitle = "发现新版本",
                        ContentMessage = $"检测到新版本：{remoteVersion}\n点击确定前往下载",
                        ButtonDefinitions = new[]
                        {
                            new ButtonDefinition { Name = "确定", IsDefault = true }
                        },
                        Icon = Icon.Info
                    }).Show();

                if (result == "确定" && !string.IsNullOrEmpty(downloadUrl))
                {
                    Process.Start(new ProcessStartInfo(downloadUrl) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            // 可选：记录日志或忽略
            System.Diagnostics.Debug.WriteLine("检查更新失败: " + ex);
        }
    }
}