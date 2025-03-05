// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Windows;

namespace VRCOSC.Modules.HTTP.UI;

public partial class ModuleSettingView
{
    private readonly ModuleSetting moduleSetting;

    public ModuleSettingView(HTTPModule _, ModuleSetting moduleSetting)
    {
        this.moduleSetting = moduleSetting;

        InitializeComponent();

        DataContext = moduleSetting;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var instance = element.Tag;

        moduleSetting.Remove(instance);
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e)
    {
        moduleSetting.Add();
    }
}
