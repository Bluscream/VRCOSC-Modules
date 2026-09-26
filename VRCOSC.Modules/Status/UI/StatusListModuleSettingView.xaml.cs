// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

using System.Windows;

namespace Bluscream.Modules.Status.UI;

public partial class StatusListModuleSettingView
{
    private readonly StatusListModuleSetting moduleSetting;

    public StatusListModuleSettingView(StatusModule _, StatusListModuleSetting moduleSetting)
    {
        this.moduleSetting = moduleSetting;
        InitializeComponent();
        DataContext = moduleSetting;
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e) => moduleSetting.Add();

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        moduleSetting.Remove((StatusEntry)element.Tag);
    }
}
