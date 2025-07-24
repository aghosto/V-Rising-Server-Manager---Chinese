using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using static System.Int32;


namespace VRisingServerManager;
class ModsList
{
    public ObservableCollection<ModInfo> ModList { get; set; } = new();
}

public class ModInfo : PropertyChangedBase
{
    private bool _installed = false;
    public bool Installed
    {
        get => _installed;
        set => SetField(ref _installed, value);
    }
    private bool _downloaded = false;
    public bool Downloaded
    {
        get => _downloaded;
        set => SetField(ref _downloaded, value);
    }
    public string Name { get; set; }
    public string Full_Name { get; set; }
    public string Owner { get; set; }
    public string Package_Url { get; set; }
    public DateTime Date_Created { get; set; }
    public DateTime Date_Updated { get; set; }
    public string Uuid4 { get; set; }
    public int RatingScore { get; set; }
    public bool IsPinned { get; set; }
    public bool IsDeprecated { get; set; }
    public bool HasNsfwContent { get; set; }
    public List<string> Categories { get; set; }
    public List<Version> Versions { get; set; }
    public string LocalVersion { get; set; }
    public bool UpdateAvailable { get; set; } = false;
    public string ToolTipText { get; set; } = "未下载";
    public int OriginalIndex { get; set; }
}

public class Version
{
    public string Name { get; set; }
    public string Full_Name { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    public string Version_Number { get; set; }
    public List<string> Dependencies { get; set; }
    public string Download_Url { get; set; }
    public int Downloads { get; set; }
    public DateTime DateCreated { get; set; }
    public string Website_Url { get; set; }
    public bool IsActive { get; set; }
    public string Uuid4 { get; set; }
    public int File_Size { get; set; }
}

public class VersionToolTipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string localVersion = value as string;
        if (string.IsNullOrEmpty(localVersion))
            return "无本地版本";

        return $"本地版本: {localVersion}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// 布尔值到Visibility转换器
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // 如果值为true，返回Visible；否则返回Collapsed
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// 字符串到Visibility转换器
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            // 如果字符串不为空，返回Visible；否则返回Collapsed
            return !string.IsNullOrEmpty(stringValue) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
