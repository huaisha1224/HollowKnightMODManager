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
        // ����Ҫ�ֶ����� DataContext��ReactiveUserControl ���Զ�����
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}