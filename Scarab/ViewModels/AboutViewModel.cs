using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Scarab.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> Donate { get; set; } = ReactiveCommand.Create(_Donate);

    public ReactiveCommand<Unit, Unit> Download { get; set; } = ReactiveCommand.Create(_Download);
    public ReactiveCommand<Unit, Unit> OpenLogs { get; set; } = ReactiveCommand.Create(_OpenLogs);

    public string Version { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

    public static string OSString => $"{OS} {Environment.OSVersion.Version}";

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
}