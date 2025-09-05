using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Scarab.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> Donate { get; set; } = ReactiveCommand.Create(_Donate);

    public ReactiveCommand<Unit, Unit> Download { get; set; } = ReactiveCommand.Create(_Download);
    public ReactiveCommand<Unit, Unit> OpenLogs { get; set; } = ReactiveCommand.Create(_OpenLogs);
    public ReactiveCommand<Unit, Unit> OpenSource { get; set; } = ReactiveCommand.Create(_OpenSource);

    public string Version { get; } = GetVersion();
    public string FileVersion { get; } = GetFileVersion();

    public static string OSString => $"{OS} {Environment.OSVersion.Version}";

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

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static string OS
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return "Windows";
            if (OperatingSystem.IsMacOS())
                return "macOS";
            if (OperatingSystem.IsLinux())
                return "Linux";
            return "Unknown";
        }
    }

    private static void _Donate() 
    {
        Process.Start(new ProcessStartInfo("https://www.bilibili.com/video/BV1JrarzEEQD") { UseShellExecute = true });
    }

    private static void _Download()
    {
        Process.Start(new ProcessStartInfo("https://hs2049.cn") { UseShellExecute = true });
    }

    private static void _OpenLogs()
    {
        Process.Start(new ProcessStartInfo(Settings.GetOrCreateDirPath()) { UseShellExecute = true });
    }

    private static void _OpenSource()
    {
        Process.Start(new ProcessStartInfo("https://github.com/huaisha1224/HollowKnightMODManager") { UseShellExecute = true });
    }
}