using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Scarab.ViewModels;

namespace Scarab.Views;

public partial class HelpView : ReactiveUserControl<HelpViewModel>
{
    public HelpView()
    {
        InitializeComponent();
        // 不需要手动设置 DataContext，ReactiveUserControl 会自动处理
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}