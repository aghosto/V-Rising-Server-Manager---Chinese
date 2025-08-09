using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Windows.Navigation;
using ModernWpf.Controls;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static VRisingServerManager.LogManager;

namespace VRisingServerManager;
/// <summary>
/// Interaction logic for Window1.xaml
/// </summary>
public partial class AdminManager : Window
{
    Server serverToManage;
    MainSettings _mainSettings;

    // 定义事件：当管理员列表被修改并关闭窗口时触发
    public event Action AdminListUpdated;

    // 标记管理员列表是否有修改（用于判断是否需要触发事件）
    private bool _hasChanges = false;

    public AdminManager(Server server)
    {
        InitializeComponent();
        if (server == null)
            throw new ArgumentNullException(nameof(server), "服务器实例不能为null");

        serverToManage = server;
        ReloadList(Path.Combine(server.Path, "SaveData", "Settings", "adminlist.txt"));
        AdminList.SelectionChanged += AdminList_SelectionChanged;

        // 窗口关闭时检查是否需要触发更新事件
        Closing += (s, e) =>
        {
            if (_hasChanges)
            {
                AdminListUpdated?.Invoke(); // 触发事件，通知主窗口
            }
        };
    }

    private void AdminList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (AdminList.SelectedIndex != -1)
        {
            RemoveAdminButton.IsEnabled = true;
        }
        else
        {
            RemoveAdminButton.IsEnabled = false;
        }
    }

    

    public void ReloadList(string filePath)
    {
        AdminList.Items.Clear();
        using (StreamReader sr = new StreamReader(filePath))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                AdminList.Items.Add(line);
            }
            sr.Close();
        }
        if (AdminList.Items.Count > 0)
            AdminList.SelectedIndex = AdminList.Items.Count;
    }

    private async void AddAdminButton_Click(object sender, RoutedEventArgs e)
    {
        string input = AdminToAdd.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            await ShowError("SteamID为空，请输入ID或链接");
            return;
        }

        if (TryExtractSteamId(input, out string steamId))
        {
            if (ulong.TryParse(steamId, out _))
            {
                if (!AdminList.Items.Contains(steamId))
                {
                    AdminList.Items.Add(steamId);
                    AdminToAdd.Clear();
                    AdminList.SelectedIndex = AdminList.Items.Count;

                    return;
                }
                else
                {
                    await ShowError("该ID已在管理员列表中");
                }
                _hasChanges = true;
            }
            else
            {
                await ShowError("提取的ID无效，请检查输入");
            }
        }
        else
        {
            await ShowError("输入格式错误，请输入64位SteamID或Steam个人主页链接");
        }
    }

    private async void RemoveAdminButton_Click(object sender, RoutedEventArgs e)
    {
        if (AdminList.SelectedIndex != -1)
        {
            AdminList.Items.RemoveAt(AdminList.SelectedIndex);
            if (AdminList.Items.Count > 0)
                AdminList.SelectedIndex = AdminList.Items.Count;
        }
        else
        {
            ContentDialog closeFileDialog = new()
            {
                Content = "请选择需要移除的ID",
                PrimaryButtonText = "是",
            };
            await closeFileDialog.ShowAsync();
            //MessageBox.Show("请选择需要移除的ID", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
            
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start("explorer.exe", "https://steamid.io/lookup");
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (serverToManage == null)
        {
            await ShowError("服务器信息为空，无法保存管理员列表");
            return;
        }
        string adminListPath = Path.Combine(serverToManage.Path, "SaveData", "Settings", "adminlist.txt");
        if (string.IsNullOrEmpty(adminListPath))
        {
            await ShowError("管理员列表路径无效");
            return;
        }
        try
        {
            string directory = Path.GetDirectoryName(adminListPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (StreamWriter saveFile = new StreamWriter(adminListPath))
            {
                foreach (var item in AdminList.Items)
                {
                    if (item != null)
                    {
                        saveFile.WriteLine(item.ToString());
                    }
                }
            }
            _hasChanges = true; 
            Close();
        }
        catch (Exception ex)
        {
            await ShowError($"保存失败：{ex.Message}\n路径：{adminListPath}");
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        ReloadList(serverToManage.Path + @"\SaveData\Settings\adminlist.txt");
    }

    private bool TryExtractSteamId(string input, out string steamId)
    {
        steamId = null;

        if (Regex.IsMatch(input, @"^\d+$"))
        {
            steamId = input;
            return true;
        }

        // 正则匹配获取一下玩家的64位steamID
        var match = Regex.Match(input, @"steamcommunity\.com/profiles/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            steamId = match.Groups[1].Value;
            return true;
        }

        return false;
    }

    private async Task ShowError(string message)
    {
        var dialog = new ContentDialog
        {
            Content = message,
            Title = "操作失败",
            CloseButtonText = "确定"
        };
        await dialog.ShowAsync();
    }
}
