using System.Diagnostics;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using Avalonia.Controls;
using Avalonia;
using MessageBox.Avalonia.Models;
// Resources is a field in Avalonia UserControls, so alias it for brevity
using Localization = Scarab.Resources;

namespace Scarab.Views;

[UsedImplicitly]
public partial class ModPageView : ReactiveUserControl<ModPageViewModel>
{
    private WindowNotificationManager? _notify;

    public ModPageView()
    {
        InitializeComponent();

        UserControl.KeyDown += OnKeyDown;

        this.WhenActivatedVM((vm, d) =>
        {
            // 初始化通知管理器
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                _notify = new WindowNotificationManager(window)
                {
                    Position = NotificationPosition.BottomRight, // 修改为底部居中显示
                    MaxItems = 3
                };
            }

            vm.CompletedAction += OnComplete;
            vm.ExceptionRaised += OnError;

            this.WhenAnyValue(x => x.TagBox.SelectionBoxItem)
                .Subscribe(x => vm.SelectedTag = (Tag) (x ?? Models.Tag.All))
                .DisposeWith(d);
        });
    }

    private async void OnError(ModPageViewModel.ModAction act, Exception e, ModItem? m)
    {
        Trace.TraceError($"Failed action {act} for {m?.Name ?? "null item"}, ex: {e}");

        switch (e)
        {
            case HttpRequestException:
            {
                // 获取主窗口以便居中显示消息框
                Window? mainWindow = null;
                try
                {
                    mainWindow = (Window?)Application.Current?.ApplicationLifetime switch
                    {
                        Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
                        _ => null
                    };
                }
                catch
                {
                    // 如果无法获取主窗口，就使用默认设置
                }

                // 显示错误信息给用户
                var messageBox = MessageBoxManager.GetMessageBoxCustomWindow(
                    new MessageBoxCustomParams
                    {
                        ContentTitle = "网络错误",
                        ContentMessage = "在安装时发生网络错误，\n\n请先加速GitHub之后重新启动MOD安装器",
                        ButtonDefinitions = new[]
                        {
                            new ButtonDefinition { Name = "确定", IsDefault = true }
                        },
                        Icon = Icon.Error,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    });
                
                // 如果获取到了主窗口，则在主窗口中央显示，否则使用默认位置
                if (mainWindow != null)
                {
                    var result = await messageBox.Show(mainWindow); // 将主窗口作为所有者传入
                    if (result == "确定")
                    {
                        Environment.Exit(-1);
                    }
                }
                else
                {
                    var result = await messageBox.Show(); // 使用默认显示方式
                    if (result == "确定")
                    {
                        Environment.Exit(-1);
                    }
                }

                break;
            }

            case HashMismatchException hashEx:
            {
                _notify?.Show(new Notification(
                    $"Failed to {act} {m?.Name ?? string.Empty}!",
                    string.Format(
                        Localization.MLVM_DisplayHashMismatch_Msgbox_Text,
                        hashEx.Name,
                        hashEx.Actual,
                        hashEx.Expected
                    ),
                    NotificationType.Error
                ));

                break;
            }

            default:
            {
                // TODO: on click event.
                _notify?.Show(new Notification(
                    // TODO: stringify lmao
                    $"Failed to {act} {m?.Name ?? string.Empty}!",
                    e.ToString(),
                    NotificationType.Error
                ));

                break;
            }
        }
    }

    //新增安装卸载提示文案
    private void OnComplete(ModPageViewModel.ModAction act, ModItem mod)
    {
        string act_s = act switch
        {
            ModPageViewModel.ModAction.Install => Localization.NOTIFY_Installed, // 直接写中文或用资源
            ModPageViewModel.ModAction.Update => Localization.NOTIFY_Updated,
            ModPageViewModel.ModAction.Uninstall => Localization.NOTIFY_Uninstalled,
            // We don't display notifications for toggling - but keep an explicit arm for the sake of total matching
            ModPageViewModel.ModAction.Toggle => throw new ArgumentOutOfRangeException(nameof(act), act, null),
            _ => throw new ArgumentOutOfRangeException(nameof(act), act, null)
        };

        _notify?.Show(new Notification(act_s, mod.Name, NotificationType.Information));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F5:
                ViewModel?.UpdateAll.Execute().Subscribe();
                break;
            case Key.Space:
                if (ViewModel?.SelectedModItem is { } item)
                {
                    if (item.Installed)
                        ViewModel.OnUninstall.Execute(item).Subscribe();
                    else
                        ViewModel.OnInstall.Execute(item).Subscribe();
                }

                break;
        }
    }
}