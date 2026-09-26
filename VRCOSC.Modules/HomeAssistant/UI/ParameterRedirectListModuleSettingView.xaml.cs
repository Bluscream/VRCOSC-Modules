// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Bluscream.Modules.HomeAssistant.UI;

public partial class ParameterRedirectListModuleSettingView
{
    private readonly ParameterRedirectListModuleSetting moduleSetting;

    public IEnumerable<RedirectConversion> ConversionItemsSource => Enum.GetValues<RedirectConversion>();

    public ParameterRedirectListModuleSettingView(HomeAssistantModule _, ParameterRedirectListModuleSetting moduleSetting)
    {
        this.moduleSetting = moduleSetting;
        InitializeComponent();
        DataContext = moduleSetting;
    }

    private void AddButton_OnClick(object sender, RoutedEventArgs e) => moduleSetting.Add();

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        moduleSetting.Remove((ParameterRedirect)element.Tag);
    }
}
