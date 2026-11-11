using System;
using Avalonia.Markup.Xaml;

namespace PCRemote.Desktop.Localization;

public class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationManager.GetString(Key);
    }
}