using ReactiveUI;
using System.Reactive;
using System.Diagnostics;
using System.Reflection;

namespace Scarab.ViewModels;

public class HelpViewModel : ReactiveObject
{
    public string Version { get; } = GetVersion();
    public string FileVersion { get; } = GetFileVersion();
    public ReactiveCommand<Unit, Unit> OpenSource { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHub { get; }
    public ReactiveCommand<Unit, Unit> OpenBilibili { get; }

    public HelpViewModel()
    {
        OpenSource = ReactiveCommand.Create(() =>
        {
            // 打开GitHub源码页
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/hk-modding/modlinks",
                UseShellExecute = true
            });
        });
        
        OpenGitHub = ReactiveCommand.Create(() =>
        {
            // 打开项目GitHub页面
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/huaisha1224/HollowKnightMODManager",
                UseShellExecute = true
            });
        });
        
        OpenBilibili = ReactiveCommand.Create(() =>
        {
            // 打开B站怀沙2049页面
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://space.bilibili.com/37443749",
                UseShellExecute = true
            });
        });
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "Unknown";
    }

    private static string GetFileVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
        return fileVersionAttr?.Version ?? "Unknown";
    }
}