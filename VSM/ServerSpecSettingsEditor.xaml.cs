using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Text.Json;
using System.Collections.ObjectModel;
using ModernWpf.Controls;
using VRisingServerManager.Controls;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Net.Http;
using System.Diagnostics;


namespace VRisingServerManager
{
    /// <summary>
    /// ServerSpecSettingsEditor.xaml 的交互逻辑
    /// </summary>
    public partial class ServerSpecSettingsEditor : Window
    {
        private ServerSpecSettings specServicesSettings;
        private JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly ObservableCollection<Server> servers;

        public ServerSpecSettingsEditor(ObservableCollection<Server> sentServers, bool autoLoad = false, int indexToLoad = -1)
        {
            servers = sentServers;
            specServicesSettings = new ServerSpecSettings();
            DataContext = specServicesSettings;
            InitializeComponent();

            if (autoLoad == true && indexToLoad != -1 && servers.Count > 0)
            {
                AutoLoad(indexToLoad);
            }
        }
        private async void AutoLoad(int serverIndex)
        {
            string fileToLoad = servers[serverIndex].Path + @"\SaveData\Settings\ServerSpecSettings.json";
            if (!File.Exists(fileToLoad))
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "错误",
                    Content = $"加载服务器特殊配置文件失败：{fileToLoad}\n请确认该服务器存在",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                //File.Create(fileToLoad);
                return;
            }

            using (StreamReader reader = new StreamReader(fileToLoad))
            {
                string LoadedJSON = reader.ReadToEnd();
                ServerSpecSettings LoadedSettings = System.Text.Json.JsonSerializer.Deserialize<ServerSpecSettings>(LoadedJSON);
                specServicesSettings = LoadedSettings;
                DataContext = specServicesSettings;
            }
        }
        private async void SpecSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (servers.Count > 0)
            {
                //ContentDialog yesNoDialog = new()
                //{
                //    Content = "是否保存到服务器？如果原始文件存在，将创建其备份。",
                //    PrimaryButtonText = "是",
                //    SecondaryButtonText = "否"
                //};

                //if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                //{
                    EditorSaveDialog dialog = new(servers)
                    {
                        PrimaryButtonText = "保存",
                        CloseButtonText = "取消"
                    };
                    Server server;

                    if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                    {
                        server = dialog.GetServer();
                        if (!Directory.Exists(server.Path + @"\SaveData\Settings"))
                        {
                            ContentDialog newDialog = new()
                            {
                                Content = "在服务器路径中未找到SaveData文件夹。请确保您已启动服务器一次。",
                                PrimaryButtonText = "好的"
                            };
                            await newDialog.ShowAsync();
                            return;
                        }
                        if (File.Exists(server.Path + @"\SaveData\Settings\ServerSpecSettings.json"))
                            File.Copy(server.Path + @"\SaveData\Settings\ServerSpecSettings.json", server.Path + @"\SaveData\Settings\ServerSpecSettings.bak", true);

                        string SettingsJSON = System.Text.Json.JsonSerializer.Serialize(specServicesSettings, serializerOptions);
                        File.WriteAllText(server.Path + @"\SaveData\Settings\ServerSpecSettings.json", SettingsJSON);

                        ContentDialog closeFileDialog = new()
                        {
                            Content = "文件成功保存于：\n" + server.Path + @"\SaveData\Settings\ServerSpecSettings.json",
                            PrimaryButtonText = "是",
                        };
                        await closeFileDialog.ShowAsync();
                        return;
                    }
                    else
                    {
                        return;
                    }
                //}
            }

            try
            {
                string SettingsJSON = System.Text.Json.JsonSerializer.Serialize(specServicesSettings, serializerOptions);
                SaveFileDialog SaveSettingsDialog = new SaveFileDialog
                {
                    Filter = "\"JSON files\"|*.json",
                    DefaultExt = "json",
                    FileName = "ServerSpecSettings.json",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };
                if (SaveSettingsDialog.ShowDialog() == true)
                {
                    if (File.Exists(SaveSettingsDialog.FileName))
                    {
                        File.Copy(SaveSettingsDialog.FileName, SaveSettingsDialog.FileName + ".bak", true);
                    }
                    File.WriteAllText(SaveSettingsDialog.FileName, SettingsJSON, System.Text.Encoding.UTF8);
                    await Task.Delay(1000);
                    MessageBox.Show("文件成功保存于：\n" + SaveSettingsDialog.FileName);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void FileMenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
