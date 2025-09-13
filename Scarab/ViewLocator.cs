using System.Diagnostics;
using Avalonia.Controls.Templates;
using Scarab.Views;

namespace Scarab;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        Debug.Assert(data != null, nameof(data) + " != null");
        
        // 特殊处理HelpViewModel和AboutViewModel
        if (data is Scarab.ViewModels.HelpViewModel)
        {
            return new HelpView { DataContext = data };
        }
        
        if (data is Scarab.ViewModels.AboutViewModel)
        {
            return new AboutView { DataContext = data };
        }
        
        string? name = data.GetType().FullName?.Replace("ViewModel", "View");

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException($"{nameof(name)}: {name}");

        var type = Type.GetType(name);

        if (type == null) 
            return new TextBlock { Text = "Not Found: " + name };
            
        var ctrl = (Control) Activator.CreateInstance(type)!;
        ctrl.DataContext = data;

        return ctrl;

    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || data is Scarab.ViewModels.HelpViewModel || data is Scarab.ViewModels.AboutViewModel;
    }
}