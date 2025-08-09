using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static VRisingServerManager.LogManager;

//localhost:9090/metrics

namespace VRisingServerManager;
/// <summary>
/// Interaction logic for ModManager.xaml
/// </summary>
public partial class ModManager : Window
{
    MainWindow mainWindow = Application.Current.MainWindow as MainWindow;

    static HttpClientHandler handler = new()
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    };
    HttpClient hClient = new(handler)
    {
        BaseAddress = new Uri("https://v-rising.thunderstore.io/")
    };

    ModsList Mods = new();
    MainSettings VsmSettings = new();
    private bool DownloadInProgress = false;

    // 版本解析正则表达式
    private readonly Regex _versionRegex = new Regex(@"(\d+(\.\d+){0,3})");

    public ModManager(MainSettings mainSettings)
    {
        VsmSettings = mainSettings;
        hClient.DefaultRequestHeaders.Accept.Clear();
        hClient.DefaultRequestHeaders.Add("Accept", "application/json");
        hClient.DefaultRequestHeaders.Add("X-CSRFToken", "142eQij51Te4FSYC7q6fWFxknq7MomDvyc6iX27hgJxt8kh2BGZZBnU6frrW3DYx");

        InitializeComponent();

        RefreshModList();

        ServerComboBox.DataContext = VsmSettings;

        if (VsmSettings.Servers.Count > 0)
        {
            ServerComboBox.SelectedIndex = 0;
        }
    }

    private async Task RefreshModList()
    {
        try
        {
            // 获取远程Mod列表
            HttpResponseMessage response = await hClient.GetAsync("api/v1/package/");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStreamAsync();
            var json = await JsonSerializer.DeserializeAsync<ObservableCollection<ModInfo>>(result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // 先清空
            Mods.ModList.Clear();

            if (json != null)
            {
                foreach (var modInfo in json)
                {
                    modInfo.LocalVersion = modInfo.Versions[0].Version_Number;
                    Mods.ModList.Add(modInfo);
                }
            }

            if (ModsDataGrid.ItemsSource == null)
            {
                ModsDataGrid.ItemsSource = Mods.ModList;
            }

            ModsDataGrid.SelectedIndex = -1;

            // 处理重复的UUID，只保留第一个出现的记录
            var downloadedModsMap = VsmSettings.DownloadedMods
                .Where(m => m.Downloaded)
                .GroupBy(m => m.Uuid4) 
                .Select(g => g.First())  
                .ToDictionary(m => m.Uuid4);  

            foreach (ModInfo modInfo in Mods.ModList)
            {
                if (downloadedModsMap.TryGetValue(modInfo.Uuid4, out Mod localMod))
                {
                    modInfo.Downloaded = true;
                    modInfo.LocalVersion = localMod.LocalVersion;

                    Server currentServer = (Server)ServerComboBox.SelectedItem;
                    string serverVersion = null;
                    if (currentServer != null &&
                        currentServer.InstalledModVersions.TryGetValue(modInfo.Uuid4, out var ver))
                    {
                        serverVersion = ver;
                    }
                    else
                    {
                        serverVersion = localMod.LocalVersion;
                    }

                    modInfo.UpdateAvailable = !string.IsNullOrEmpty(serverVersion) &&
                        IsVersionOlder(serverVersion, modInfo.Versions[0].Version_Number);

                    // 光标悬浮显示文字
                    modInfo.ToolTipText = modInfo.UpdateAvailable
                        ? $"服务器版本: {serverVersion}\n最新版本: {modInfo.Versions[0].Version_Number}\n有可用更新"
                        : $"服务器版本: {serverVersion}\n最新版本: {modInfo.Versions[0].Version_Number}\n已是最新";
                }
                else
                {
                    modInfo.Downloaded = false;
                    modInfo.ToolTipText = "未下载";
                }
            }

            Server server = (Server)ServerComboBox.SelectedItem;
            var installedModsSet = new HashSet<string>(server.InstalledMods);

            foreach (ModInfo modInfo in Mods.ModList)
            {
                modInfo.Installed = installedModsSet.Contains(modInfo.Uuid4);
            }

            if (Mods.ModList.Count > 0)
            {
                ModsDataGrid.SelectedIndex = 0;
            }
            
            MainSettings.Save(VsmSettings);
        }
        catch (HttpRequestException ex)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole, $"获取Mod列表失败: {ex.Message}", Brushes.Red);

            if (ex.InnerException != null)
            {
                mainWindow.ShowLogMsg(LogType.MainConsole, $"内部异常: {ex.InnerException.Message}", Brushes.Red);

                if (ex.InnerException is System.Security.Authentication.AuthenticationException authEx)
                {
                    mainWindow.ShowLogMsg(LogType.MainConsole, $"SSL认证错误: {authEx.Message}，请检查你的网络连接", Brushes.Red);
                }
            }
        }
        catch (Exception ex)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole, $"刷新Mod列表时发生未知错误: {ex.Message}", Brushes.Red);
        }
    }

    private bool IsVersionOlder(string installedVersion, string latestVersion)
    {
        try
        {
            var cleanInstalled = _versionRegex.Match(installedVersion).Value;
            var cleanLatest = _versionRegex.Match(latestVersion).Value;

            var installedParts = cleanInstalled.Split('.').Select(int.Parse).ToList();
            var latestParts = cleanLatest.Split('.').Select(int.Parse).ToList();

            int maxLength = Math.Max(installedParts.Count, latestParts.Count);
            while (installedParts.Count < maxLength) installedParts.Add(0);
            while (latestParts.Count < maxLength) latestParts.Add(0);

            for (int i = 0; i < maxLength; i++)
            {
                if (installedParts[i] < latestParts[i])
                    return true;
                if (installedParts[i] > latestParts[i])
                    return false; 
            }

            return false; 
        }
        catch (Exception ex)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole,
                $"版本比较失败: {installedVersion} vs {latestVersion}, 错误: {ex.Message}",
                Brushes.Orange);
            return true;
        }
    }

    private async Task InstallBepInEx(Server server)
    {
        if (server.Runtime.State == ServerRuntime.ServerState.运行中)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 卸载错误",
                Content = $"{server.vsmServerName} 正在运行，请在安装BepInEx之前终止服务器。",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return;
        }

        ContentDialog yesNoDialog = new()
        {
            Owner = this,
            Title = "BepInEx - 安装",
            Content = $"BepInEx现在将下载并安装在{server.vsmServerName}上。如果您以前已经手动安装过，请创建备份，因为所有文件都将被覆盖。\n\n是否开始安装？",
            PrimaryButtonText = "是",
            CloseButtonText = "否",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
        {
            try
            {
                string workingDir = Directory.GetCurrentDirectory();
                ModInfo bixMod = new();
                foreach (ModInfo mod in Mods.ModList)
                {
                    if (mod.Uuid4 == "b86fcaaf-297a-45c8-82a0-fcbd7806fdc4")
                        bixMod = mod;
                }

                foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
                {
                    if (downloadedMod.Uuid4 == bixMod.Uuid4 && downloadedMod.Downloaded == true)
                    {
                        if (Directory.Exists(workingDir + @"\Mods\temp"))
                            Directory.Delete(workingDir + @"\Mods\temp", true);

                        Directory.CreateDirectory(workingDir + @"\Mods\temp");
                        ZipFile.ExtractToDirectory(workingDir + @"\Mods\" + downloadedMod.ArchiveName, workingDir + @"\Mods\temp", true);

                        string[] dirs1 = Directory.GetDirectories(workingDir + @"\Mods\temp\BepInExPack_V_Rising");
                        string[] files1 = Directory.GetFiles(workingDir + @"\Mods\temp\BepInExPack_V_Rising");
                        foreach (string file in files1)
                            File.Move(file, server.Path + @"\" + Path.GetFileName(file), true);

                        foreach (string dir in dirs1)
                        {
                            DirectoryInfo dirInfo = new(dir);
                            if (Directory.Exists(server.Path + @"\" + dirInfo.Name))
                                Directory.Delete(server.Path + @"\" + dirInfo.Name, true);
                            Directory.Move(dir, server.Path + @"\" + dirInfo.Name);
                        }

                        Directory.Delete(workingDir + @"\Mods\temp", true);
                        server.InstalledMods.Add(bixMod.Uuid4);
                        server.BepInExInstalled = true;
                        server.BepInExVersion = bixMod.Versions[0].Version_Number;
                        bixMod.Installed = true;
                        MainSettings.Save(VsmSettings);

                        _ = new ContentDialog
                        {
                            Owner = this,
                            Title = "BepInEx - 安装",
                            Content = $"BepInEx 已安装于 {server.vsmServerName}.",
                            CloseButtonText = "好的",
                            DefaultButton = ContentDialogButton.Close
                        }.ShowAsync();

                        return;
                    }
                }

                if (!Directory.Exists(workingDir + @"\Mods"))
                    Directory.CreateDirectory(workingDir + @"\Mods");

                using Stream stream = await hClient.GetStreamAsync(bixMod.Versions[0].Download_Url);
                using (var fileStream = File.Create(workingDir + @"\Mods\" + bixMod.Versions[0].Full_Name + ".zip"))
                {
                    await stream.CopyToAsync(fileStream);
                }

                Mod modToSave = new()
                {
                    Downloaded = true,
                    ArchiveName = bixMod.Versions[0].Full_Name + ".zip",
                    Uuid4 = bixMod.Uuid4
                };

                VsmSettings.DownloadedMods.Add(modToSave);
                int modIndex = Mods.ModList.IndexOf(bixMod);
                Mods.ModList[modIndex].Downloaded = true;

                if (Directory.Exists(workingDir + @"\Mods\temp"))
                    Directory.Delete(workingDir + @"\Mods\temp", true);

                Directory.CreateDirectory(workingDir + @"\Mods\temp");
                ZipFile.ExtractToDirectory(workingDir + @"\Mods\" + modToSave.ArchiveName, workingDir + @"\Mods\temp", true);

                string[] dirs = Directory.GetDirectories(workingDir + @"\Mods\temp\BepInExPack_V_Rising");
                string[] files = Directory.GetFiles(workingDir + @"\Mods\temp\BepInExPack_V_Rising");
                foreach (string file in files)
                {
                    File.Move(file, server.Path + @"\" + Path.GetFileName(file), true);
                }

                foreach (string dir in dirs)
                {
                    DirectoryInfo dirInfo = new(dir);
                    if (Directory.Exists(server.Path + @"\" + dirInfo.Name))
                        Directory.Delete(server.Path + @"\" + dirInfo.Name, true);
                    Directory.Move(dir, server.Path + @"\" + dirInfo.Name);
                }

                Directory.Delete(workingDir + @"\Mods\temp", true);
                server.InstalledMods.Add(bixMod.Uuid4);
                server.BepInExInstalled = true;
                server.BepInExVersion = bixMod.Versions[0].Version_Number;
                bixMod.Installed = true;
                MainSettings.Save(VsmSettings);

                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 安装",
                    Content = $"BepInEx 已安装于 {server.vsmServerName}。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();

                return;
            }
            catch (Exception ex)
            {
                ContentDialog yesDialog = new()
                {
                    Content = $"安装BepInEx时出错：\n + {ex.Message}",
                    PrimaryButtonText = "是",
                };
                await yesDialog.ShowAsync();
            }
        }
        else
            return;
    }

    private async Task<bool> UninstallBepInEx(Server server)
    {
        if (server.Runtime.State == ServerRuntime.ServerState.运行中)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 卸载错误",
                Content = $"{server.vsmServerName} 正在运行，请在卸载BepInEx之前终止服务器。",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return false;
        }

        ContentDialog yesNoDialog = new()
        {
            Owner = this,
            Title = "BepInEx - 卸载",
            Content = $"BepInEx现在将从 {server.vsmServerName} 中卸载。此过程是不可逆的，如果你想要创建插件或配置的备份，请在继续之前完成。所有已安装的MOD也将从服务器上删除。\n\n是否立即卸载？",
            PrimaryButtonText = "是",
            CloseButtonText = "否",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
        {
            if (Directory.Exists(server.Path + @"\BepInEx"))
                Directory.Delete(server.Path + @"\BepInEx", true);

            if (Directory.Exists(server.Path + @"\dotnet"))
                Directory.Delete(server.Path + @"\dotnet", true);

            if (File.Exists(server.Path + @"\.doorstop_version"))
                File.Delete(server.Path + @"\.doorstop_version");

            if (File.Exists(server.Path + @"\doorstop_config.ini"))
                File.Delete(server.Path + @"\doorstop_config.ini");

            if (File.Exists(server.Path + @"\winhttp.dll"))
                File.Delete(server.Path + @"\winhttp.dll");

            foreach (ModInfo mod in Mods.ModList)
            {
                if (mod.Uuid4 == "b86fcaaf-297a-45c8-82a0-fcbd7806fdc4")
                {
                    mod.Installed = false;
                    RemoveMod(mod);
                }
            }

            server.InstalledMods = new();
            server.BepInExInstalled = false;
            server.BepInExVersion = "";

            foreach (ModInfo modInfo in Mods.ModList)
                modInfo.Installed = false;

            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 卸载成功",
                Content = $"BepInEx已从 {server.vsmServerName} 中卸载。",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();

            MainSettings.Save(VsmSettings);
            return true;
        }
        else
            return false;
    }

    private async Task<bool> DownloadMod(ModInfo mod)
    {
        if (DownloadInProgress)
        {
            ContentDialog yesDialog = new()
            {
                Content = "已有另一个下载程序正在运行",
                PrimaryButtonText = "是",
            };
            await yesDialog.ShowAsync();
            return false;
        }

        DownloadInProgress = true;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressText.Text = $"正在下载：{mod.Full_Name}";
        mainWindow.ShowLogMsg(LogType.MainConsole, $"正在下载Mod：{mod.Name}", Brushes.Yellow);

        int modIndex = Mods.ModList.IndexOf(mod);
        string workingDir = Directory.GetCurrentDirectory();
        if (!Directory.Exists(workingDir + @"\Mods"))
            Directory.CreateDirectory(workingDir + @"\Mods");

        using Stream stream = await hClient.GetStreamAsync(mod.Versions[0].Download_Url);
        using (var fileStream = File.Create(workingDir + @"\Mods\" + mod.Versions[0].Full_Name + ".zip"))
        {
            await stream.CopyToAsync(fileStream);
        }

        foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
        {
            try
            {
                if (downloadedMod.Uuid4 == mod.Uuid4)
                {
                    downloadedMod.Downloaded = true;
                    downloadedMod.ArchiveName = mod.Versions[0].Full_Name + ".zip";
                    downloadedMod.LocalVersion = mod.Versions[0].Version_Number;

                    Mods.ModList[modIndex].Downloaded = true;

                    ModsDataGrid.Items.Refresh();
                    DownloadInProgress = false;
                    DownloadProgressBar.Visibility = Visibility.Hidden;
                    DownloadProgressText.Text = "";

                    MainSettings.Save(VsmSettings);

                    return true;
                }
            }
            catch (Exception ex)
            {
                ContentDialog yesDialog = new()
                {
                    Content = $"下载出错：{ex.ToString()}",
                    PrimaryButtonText = "是",
                };
                await yesDialog.ShowAsync();
                throw new Exception(ex.ToString());
            }
        }

        Mod modToSave = new()
        {
            Downloaded = true,
            ArchiveName = mod.Versions[0].Full_Name + ".zip",
            Uuid4 = mod.Uuid4,
            LocalVersion = mod.Versions[0].Version_Number
        };

        VsmSettings.DownloadedMods.Add(modToSave);
        Mods.ModList[modIndex].Downloaded = true;
        ModsDataGrid.Items.Refresh();

        MainSettings.Save(VsmSettings);

        DownloadInProgress = false;
        DownloadProgressBar.Visibility = Visibility.Hidden;
        DownloadProgressText.Text = "";

        return true;
    }

    private async Task<bool> InstallMod(ModInfo mod, Server server)
    {
        if (server.Runtime.State == ServerRuntime.ServerState.运行中)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "安装 - 错误",
                Content = $"{server.vsmServerName} 正在运行，请先终止服务器。",
                CloseButtonText = "Ok",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return false;
        }

        if (mod.Uuid4 == "f21c391c-0bc5-431d-a233-95323b95e01b")
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "安装 - 错误",
                Content = $"{mod.Name} 不支持，这是个软件，需要去文件处解压打开使用",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return false;
        }

        if (mod.Uuid4 == "b86fcaaf-297a-45c8-82a0-fcbd7806fdc4")
        {
            await InstallBepInEx(server);
            return true;
        }

        if (server.BepInExInstalled == false)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "错误 - BepInEx",
                Content = $"{server.vsmServerName} 上未安装BepInEx。请先安装BepInEx，然后再安装其他MOD。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return false;
        }

        if (mod.Full_Name == "BepInEx-BepInExPack_V_Rising")
        {
            await InstallBepInEx(server);
            return true;
        }

        //mainWindow.ShowLogMsg(LogType.MainConsole, $"正在安装 {mod.Name} 到 {server.vsmServerName}", Brushes.Yellow);

        string workingDir = Directory.GetCurrentDirectory();
        int downloadedModIndex = -1;

        foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
        {
            if (downloadedMod.Uuid4 == mod.Uuid4)
                downloadedModIndex = VsmSettings.DownloadedMods.IndexOf(downloadedMod);
        }

        if (downloadedModIndex == -1)
            return false;

        string modArchive = VsmSettings.DownloadedMods[downloadedModIndex].ArchiveName;
        if (Directory.Exists(workingDir + @"\Mods\temp"))
            Directory.Delete(workingDir + @"\Mods\temp", true);

        Directory.CreateDirectory(workingDir + @"\Mods\temp");
        ZipFile.ExtractToDirectory(workingDir + @"\Mods\" + modArchive, workingDir + @"\Mods\temp", true);

        string[] modFiles = Directory.GetFiles(workingDir + @"\Mods\temp");
        foreach (string file in modFiles)
        {
            string extension = Path.GetExtension(file);
            if (extension != ".dll")
                File.Delete(file);
            else
            {
                string fileName = @"plugins\" + Path.GetFileName(file);
                if (VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Contains(fileName) == false)
                    VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Add(fileName);
                File.Copy(file, server.Path + @"\BepInEx\" + fileName, true);
            }
        }

        if (Directory.Exists(workingDir + @"\Mods\temp\plugins"))
        {
            string[] pluginFiles = Directory.GetFiles(workingDir + @"\Mods\temp\plugins");
            foreach (string file in pluginFiles)
            {
                string extension = Path.GetExtension(file);
                if (extension != ".dll")
                    File.Delete(file);
                else
                {
                    string fileName = @"plugins\" + Path.GetFileName(file);
                    if (VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Contains(fileName) == false)
                        VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Add(fileName);
                    File.Copy(file, server.Path + @"\BepInEx\" + fileName, true);
                }
            }
        }

        if (Directory.Exists(workingDir + @"\Mods\temp\patchers"))
        {
            string[] patchersFiles = Directory.GetFiles(workingDir + @"\Mods\temp\patchers");
            foreach (string file in patchersFiles)
            {
                string extension = Path.GetExtension(file);
                if (extension != ".dll")
                    File.Delete(file);
                else
                {
                    string fileName = @"patchers\" + Path.GetFileName(file);
                    if (VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Contains(fileName) == false)
                        VsmSettings.DownloadedMods[downloadedModIndex].FileNames.Add(fileName);
                    File.Copy(file, server.Path + @"\BepInEx\" + fileName, true);
                }
            }
        }
        Directory.Delete(workingDir + @"\Mods\temp", true);

        // 记录版本和uuid4
        server.InstalledMods.Add(mod.Uuid4);
        var localMod = VsmSettings.DownloadedMods.First(m => m.Uuid4 == mod.Uuid4);
        server.InstalledModVersions[mod.Uuid4] = localMod.LocalVersion; 
        mod.Installed = true;

        MainSettings.Save(VsmSettings);
        return true;
    }

    private async Task<bool> UpdateMod(ModInfo mod, Server currentServer)
    {
        bool success = false;

        if (currentServer.Runtime.State == ServerRuntime.ServerState.运行中)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "Mod - 更新错误",
                Content = $"{currentServer.vsmServerName} 正在运行，请先终止服务器。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            mainWindow.ShowLogMsg(LogType.MainConsole, "Mod更新错误，服务器正在运行", Brushes.Red);
            return false;
        }

        try
        {
            if (!await DownloadMod(mod))
                return false;

            var latestVersion = mod.Versions[0].Version_Number;

            if (!await UninstallMod(mod, currentServer))
                return false;

            if (!await InstallMod(mod, currentServer))
                return false;

            foreach (var server in VsmSettings.Servers)
            {
                if (server == currentServer)
                    continue;

                if (server.InstalledModVersions.TryGetValue(mod.Uuid4, out var installedVersion) &&
                    IsVersionOlder(installedVersion, latestVersion))
                {
                    mainWindow.ShowLogMsg(LogType.MainConsole,
                        $"服务器 '{server.vsmServerName}' 的Mod '{mod.Name}' 有可用更新 " +
                        $"(当前: {installedVersion}, 最新: {latestVersion})",
                        Brushes.Orange);
                }
            }
            mainWindow.ShowLogMsg(LogType.MainConsole, $"Mod更新成功：{mod.Name}", Brushes.Lime);
            await RefreshModList();
            return true;
        }
        catch (Exception ex)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole, $"更新Mod时发生错误：{ex.Message}", Brushes.Red);
            success = false;
        }
        return success;
    }

    private async Task<bool> UninstallMod(ModInfo mod, Server server)
    {
        if (server.Runtime.State == ServerRuntime.ServerState.运行中)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 卸载错误",
                Content = $"{server.vsmServerName} 正在运行，请先终止服务器。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return false;
        }

        if (mod.Full_Name == "BepInEx-BepInExPack_V_Rising")
        {
            return await UninstallBepInEx(server);
        }

        mainWindow.ShowLogMsg(LogType.MainConsole, $"正在从 {server.vsmServerName} 卸载Mod：{mod.Name}", Brushes.Yellow);

        int downloadedModIndex = -1;

        foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
        {
            if (downloadedMod.Uuid4 == mod.Uuid4)
                downloadedModIndex = VsmSettings.DownloadedMods.IndexOf(downloadedMod);
        }

        if (downloadedModIndex == -1)
            return false;

        foreach (string file in VsmSettings.DownloadedMods[downloadedModIndex].FileNames)
        {
            File.Delete(server.Path + @"\BepInEx\" + file);
        }

        server.InstalledMods.RemoveAt(server.InstalledMods.IndexOf(mod.Uuid4));
        mod.Installed = false;
        MainSettings.Save(VsmSettings);
        return true;
    }

    private void RemoveMod(ModInfo mod)
    {
        foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
        {
            if (downloadedMod.Uuid4 == mod.Uuid4)
            {
                string workingDir = Directory.GetCurrentDirectory();
                if (File.Exists(workingDir + @"\Mods\" + downloadedMod.ArchiveName))
                    File.Delete(workingDir + @"\Mods\" + downloadedMod.ArchiveName);

                mod.Downloaded = false;
                //downloadedMod.ArchiveName = "";
                downloadedMod.LocalVersion = "";
                downloadedMod.Downloaded = false;

                MainSettings.Save(VsmSettings);
                break;
            }
        }
        mainWindow.ShowLogMsg(LogType.MainConsole, $"Mod {mod.Name} 已从Mods文件夹中移除", Brushes.Yellow);
    }

    private void ModsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModsDataGrid.SelectedIndex == -1)
            return;

        ModInfo modInfo = (ModInfo)ModsDataGrid.SelectedItem;
        int totalDownloads = 0;

        foreach (Version modVer in modInfo.Versions)
        {
            totalDownloads += modVer.Downloads;
        }
        DownloadsTextBlock.Text = totalDownloads.ToString();

        FileSizeTextBlock.Text = (modInfo.Versions[0].File_Size / 1024).ToString() + "kb";

        string dependencies = string.Join(", ", modInfo.Versions[0].Dependencies);
        DependenciesTextBlock.Text = dependencies;

        string categories = string.Join(", ", modInfo.Categories);
        CategoriesTextBlock.Text = categories;

        try
        {
            Uri uri = new Uri(modInfo.Versions[0].Icon);
            BitmapImage bitmap = new BitmapImage(uri);
            ModImage.Source = bitmap;
        }
        catch (Exception)
        {
            
        }
    }

    private async void ServerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Server server = (Server)ServerComboBox.SelectedItem;

        if (server.InstalledMods.Count > 0)
        {
            for (int i = 0; i < server.InstalledMods.Count; i++)
            {
                foreach (ModInfo mod in Mods.ModList)
                {
                    if (server.InstalledMods.Contains(mod.Uuid4))
                        mod.Installed = true;
                    else
                        mod.Installed = false;
                }
            }
        }
        else
        {
            foreach (ModInfo mod in Mods.ModList)
                mod.Installed = false;
        }
        await RefreshModList();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Hyperlink hyperlink = (Hyperlink)sender;
        Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true });
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
        if (mod == null) 
            return;
        bool success = await DownloadMod(mod);

        if (success)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole, $"{mod.Name} 已成功下载。", Brushes.Lime);
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
        Server server = (Server)ServerComboBox.SelectedItem;

        bool success = await InstallMod(mod, server);
        if (success)
        {
            mainWindow.ShowLogMsg(LogType.MainConsole, $"{mod.Name} 已成功安装至服务器 {server.vsmServerName}。", Brushes.Lime);
        }
    }

    private async void UpdateModButton_Click(object sender, RoutedEventArgs e)
    {
        ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
        Server server = (Server)ServerComboBox.SelectedItem;

        mainWindow.ShowLogMsg(LogType.MainConsole, $"正在更新Mod：{mod.Name}", Brushes.Yellow);
        await UpdateMod(mod, server);
    }

    private async void RefreshModlistButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModList();

        mainWindow.ShowLogMsg(LogType.MainConsole, $"Mod列表刷新成功！共加载 {Mods.ModList.Count} 个Mod", Brushes.LimeGreen);
    }

    private async void UninstallButton_click(object sender, RoutedEventArgs e)
    {
        ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
        Server server = (Server)ServerComboBox.SelectedItem;

        bool success = await UninstallMod(mod, server);
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;

        RemoveMod(mod);
    }

    private async void InstallBepInExButton_Click(object sender, RoutedEventArgs e)
    {
        Server server = (Server)ServerComboBox.SelectedItem;

        if (server.BepInExInstalled == true)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 错误",
                Content = $"BepInEx已安装在 {server.vsmServerName}。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return;
        }
        else
        {
            await InstallBepInEx(server);
        }
    }

    private void UninstallBepInExButton_Click(object sender, RoutedEventArgs e)
    {
        Server server = (Server)ServerComboBox.SelectedItem;

        if (server.BepInExInstalled == false)
        {
            _ = new ContentDialog
            {
                Owner = this,
                Title = "BepInEx - 错误",
                Content = $"{server.vsmServerName}上未安装BepInEx。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
            return;
        }
        else
        {
            UninstallBepInEx(server);
        }
    }

    // 搜索框文本变化时触发
    private void ModSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = (sender as TextBox)?.Text?.Trim().ToLower() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            // 搜索文本为空时显示所有Mod
            ModsDataGrid.ItemsSource = Mods.ModList;
        }
        else
        {
            // 过滤出名称包含搜索文本的Mod（不区分大小写）
            var filteredMods = Mods.ModList
                .Where(mod => mod.Name?.ToLower().Contains(searchText) == true)
                .ToList();

            ModsDataGrid.ItemsSource = filteredMods;
        }

        // 重置选中状态
        ModsDataGrid.SelectedIndex = -1;
    }

    private void ModDirectory_Click(object sender, RoutedEventArgs e)
    {
        Server currentServer = (Server)ServerComboBox.SelectedItem;
        //string modFilePath = Path.Combine(currentServer.Path, "BepInEx", "plugin", configFileName);


    }

    // 右键点击"mod配置文件修改器"时触发
    private void ModConfigEditor_Click(object sender, RoutedEventArgs e)
    {
        // 获取当前选中的Mod和服务器
        ModInfo selectedMod = ModsDataGrid.SelectedItem as ModInfo;
        Server currentServer = ServerComboBox.SelectedItem as Server;

        if (selectedMod == null || currentServer == null)
            return;

        // 构建配置文件路径（BepInEx/config/Mod名称.cfg）
        string configFileName = $"{selectedMod.Name}.cfg";
        string configFilePath = Path.Combine(currentServer.Path, "BepInEx", "config", configFileName);

        try
        {
            // 检查配置文件是否存在
            if (!File.Exists(configFilePath))
            {
                // 询问是否创建新文件
                var result = MessageBox.Show(
                    $"未找到配置文件：{configFileName}\n是否在BepInEx/config目录下创建新文件？",
                    "文件不存在",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result != MessageBoxResult.Yes)
                    return;

                // 确保BepInEx/config目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
                File.Create(configFilePath).Dispose(); // 创建空文件
            }

            // 读取配置文件内容
            string configContent = File.ReadAllText(configFilePath);

            // 打开配置文件编辑器窗口
            var editorWindow = new ModConfigEditor(configFilePath, configContent);
            editorWindow.Owner = this;
            editorWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开配置文件失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

