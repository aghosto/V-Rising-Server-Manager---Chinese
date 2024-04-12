﻿using ModernWpf.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

//localhost:9090/metrics

namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for ModManager.xaml
    /// </summary>
    public partial class ModManager : Window
    {
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

        public ModManager(MainSettings mainSettings)
        {
            VsmSettings = mainSettings;
            hClient.DefaultRequestHeaders.Accept.Clear();
            hClient.DefaultRequestHeaders.Add("Accept", "application/json");
            hClient.DefaultRequestHeaders.Add("X-CSRFToken", "142eQij51Te4FSYC7q6fWFxknq7MomDvyc6iX27hgJxt8kh2BGZZBnU6frrW3DYx");

            InitializeComponent();           

            RefreshModList();

            ServerComboBox.DataContext = mainSettings;

            if (VsmSettings.Servers.Count > 0)
            {
                ServerComboBox.SelectedIndex = 0;
            }            
        }

        private async Task RefreshModList()
        {
            HttpResponseMessage response = await hClient.GetAsync("api/v1/package/");
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStreamAsync();
            var json = await JsonSerializer.DeserializeAsync<ObservableCollection<ModInfo>>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true});
            ModsDataGrid.SelectedIndex = -1;
            Mods.ModList = json;
            ModsDataGrid.ItemsSource = Mods.ModList;
            ModsDataGrid.SelectedIndex = 0;

            foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
            {
                if (downloadedMod.Downloaded == true)
                {
                    foreach (ModInfo mod in Mods.ModList)
                    {                        
                        if (mod.Uuid4 == downloadedMod.Uuid4)
                        {
                            mod.Downloaded = true;
                            break;
                        }                        
                    }
                }
            }

            Server server = (Server)ServerComboBox.SelectedItem;

            if (server.InstalledMods.Count > 0)
            {
                for (int i = 0; i <= server.InstalledMods.Count - 1; i++)
                {
                    foreach (ModInfo mod in Mods.ModList)
                    {
                        if (mod.Uuid4 == server.InstalledMods[i])
                        {
                            mod.Installed = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (ModInfo mod in Mods.ModList)
                    mod.Installed = false;
            }            
        }

        private async Task InstallBepInEx(Server server)
        {
            if (server.Runtime.State == ServerRuntime.ServerState.Running)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 卸载错误",
                    Content = $"{server.Name} 正在运行，请在安装BepInEx之前终止服务器。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return;
            }

            ContentDialog yesNoDialog = new()
            {
                Owner = this,
                Title = "BepInEx - 安装",
                Content = $"BepInEx现在将下载并安装在{server.Name}上。如果您以前已经手动安装过，请创建备份，因为所有文件都将被覆盖。\n\n是否开始安装？",
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
                                Content = $"BepInEx 已安装于 {server.Name}.",
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
                        Content = $"BepInEx 已安装于 {server.Name}。",
                        CloseButtonText = "好的",
                        DefaultButton = ContentDialogButton.Close
                    }.ShowAsync();

                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("安装BepInEx时出错：\n" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }


            }
            else
                return;
        }

        private async void UninstallBepInEx(Server server)
        {
            if (server.Runtime.State == ServerRuntime.ServerState.Running)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 卸载错误",
                    Content = $"{server.Name} 正在运行，请在卸载BepInEx之前终止服务器。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return;
            }

            ContentDialog yesNoDialog = new()
            {
                Owner = this,
                Title = "BepInEx - 卸载",
                Content = $"BepInEx现在将从 {server.Name} 中卸载。此过程是不可逆的，如果您想要创建插件或配置的备份，请在继续之前完成。所有已安装的MOD也将从服务器上删除。\n\n是否立即卸载？",
                PrimaryButtonText = "时",
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
                MainSettings.Save(VsmSettings);

                foreach (ModInfo modInfo in Mods.ModList)
                    modInfo.Installed = false;

                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 卸载成功",
                    Content = $"BepInEx已从 {server.Name} 卸载。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
            else
                return;            
        }

        private async Task<bool> DownloadMod(ModInfo mod)
        {
            if (DownloadInProgress)
            {
                MessageBox.Show("已有另一个下载程序正在运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            DownloadInProgress = true;
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressText.Text = $"正在下载：{mod.Full_Name}";

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
                if (downloadedMod.Uuid4 == mod.Uuid4)
                {
                    downloadedMod.Downloaded = true;
                    downloadedMod.ArchiveName = mod.Versions[0].Full_Name + ".zip";

                    Mods.ModList[modIndex].Downloaded = true;

                    DownloadInProgress = false;
                    DownloadProgressBar.Visibility = Visibility.Hidden;
                    DownloadProgressText.Text = "";

                    MainSettings.Save(VsmSettings);

                    return true;
                }
            }

            Mod modToSave = new()
            {
                Downloaded = true,
                ArchiveName = mod.Versions[0].Full_Name + ".zip",
                Uuid4 = mod.Uuid4
            };
            VsmSettings.DownloadedMods.Add(modToSave);
            Mods.ModList[modIndex].Downloaded = true;

            MainSettings.Save(VsmSettings);

            DownloadInProgress = false;
            DownloadProgressBar.Visibility = Visibility.Hidden;
            DownloadProgressText.Text = "";

            return true;
        }

        private async Task<bool> InstallMod(ModInfo mod, Server server)
        {
            if (server.Runtime.State == ServerRuntime.ServerState.Running)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 卸载错误",
                    Content = $"{server.Name} 正在运行，请先终止服务器。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return false;
            }

            if (mod.Uuid4 == "f21c391c-0bc5-431d-a233-95323b95e01b" || mod.Uuid4 == "b86fcaaf-297a-45c8-82a0-fcbd7806fdc4")
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "安装 - 错误",
                    Content = $"{mod.Full_Name} 不支持。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return false;
            }

            if (server.BepInExInstalled == false)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "错误 - BepInEx",
                    Content = $"{server.Name} 上未安装BepInEx。请先安装BepInEx，然后再安装其他MOD。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return false;
            }

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

            server.InstalledMods.Add(mod.Uuid4);
            MainSettings.Save(VsmSettings);
            mod.Installed = true;
            return true;
        }

        private async Task<bool> UninstallMod(ModInfo mod, Server server)
        {
            if (server.Runtime.State == ServerRuntime.ServerState.Running)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 卸载错误",
                    Content = $"{server.Name} 正在运行，请先终止服务器。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return false;
            }

            if (mod.Full_Name == "BepInEx-BepInExPack_V_Rising")
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "BepInEx - 错误",
                    Content = $"请从上方的菜单安装/卸载BepInEx。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return false;
            }

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

        private async void RemoveMod(ModInfo mod)
        {
            foreach (Mod downloadedMod in VsmSettings.DownloadedMods)
            {
                if (downloadedMod.Uuid4 == mod.Uuid4)
                {
                    string workingDir = Directory.GetCurrentDirectory();
                    if (File.Exists(workingDir + @"\Mods\" + downloadedMod.ArchiveName))
                        File.Delete(workingDir + @"\Mods\" + downloadedMod.ArchiveName);

                    mod.Downloaded = false;
                    downloadedMod.ArchiveName = "";
                    downloadedMod.Downloaded = false;
                    MainSettings.Save(VsmSettings);
                    break;
                }
            }

            _ = new ContentDialog
            {
                Owner = this,
                Title = "移除Mod",
                Content = $"{mod.Full_Name} 已移除。",
                CloseButtonText = "好的",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
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

            Uri uri = new Uri(modInfo.Versions[0].Icon);
            BitmapImage bitmap = new BitmapImage(uri);
            ModImage.Source = bitmap;
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
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Hyperlink hyperlink = (Hyperlink)sender;
            Process.Start("explorer.exe", hyperlink.NavigateUri.ToString());
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
            if (mod == null) return;
            bool success = await DownloadMod(mod);

            if (success)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "Mod下载",
                    Content = $"{mod.Full_Name} 已成功下载。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
            Server server = (Server)ServerComboBox.SelectedItem;

            bool success = await InstallMod(mod, server);
            if (success)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "安装Mod",
                    Content = $"{mod.Full_Name} 已安装至 {server.Name}。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
        }

        private async void RefreshModlistButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshModList();
        }

        private async void UninstallButton_click(object sender, RoutedEventArgs e)
        {
            ModInfo mod = (ModInfo)ModsDataGrid.SelectedItem;
            Server server = (Server)ServerComboBox.SelectedItem;

            bool success = await UninstallMod(mod, server);
            if (success)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "卸载Mod",
                    Content = $"{mod.Full_Name} 已从 {server.Name} 卸载。",
                    CloseButtonText = "好的",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
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
                    Content = $"BepInEx已安装在 {server.Name}。",
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
                    Content = $"{server.Name}上未安装BepInEx。",
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
    }
}
