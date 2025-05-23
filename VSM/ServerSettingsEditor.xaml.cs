﻿using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Text.Json;
using System.Collections.ObjectModel;
using ModernWpf.Controls;
using VRisingServerManager.Controls;
using Newtonsoft.Json;
using System.Threading.Tasks;


namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for ServerSettingsEditor.xaml
    /// </summary>
    public partial class ServerSettingsEditor : Window
    {
        private ServerSettings serverSettings;
        private JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly ObservableCollection<Server> servers;

        public ServerSettingsEditor(ObservableCollection<Server> sentServers, bool autoLoad = false, int indexToLoad = -1)
        {
            servers = sentServers;
            serverSettings = new ServerSettings();
            DataContext = serverSettings;
            InitializeComponent();

            if (autoLoad == true && indexToLoad != -1 && servers.Count > 0)
            {
                AutoLoad(indexToLoad);
            }
        }

        private void AutoLoad(int serverIndex)
        {
            string fileToLoad = servers[serverIndex].Path + @"\SaveData\Settings\ServerHostSettings.json";
            if (!File.Exists(fileToLoad))
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "错误",
                    Content = $"加载服务器连接配置文件失败：{fileToLoad} ",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return;
            }
                
            using (StreamReader reader = new StreamReader(fileToLoad))
            {
                string LoadedJSON = reader.ReadToEnd();
                ServerSettings LoadedSettings = System.Text.Json.JsonSerializer.Deserialize<ServerSettings>(LoadedJSON);
                serverSettings = LoadedSettings;
                DataContext = serverSettings;
            }
        }

        private void FileMenuLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? FileToLoad = "temp";
                OpenFileDialog OpenSettingsDialog = new OpenFileDialog
                {
                    Filter = "\"JSON files\"|*.json",
                    DefaultExt = "json",
                    FileName = "ServerHostSettings.json",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };
                if (OpenSettingsDialog.ShowDialog() == true && FileToLoad != null)
                {
                    FileToLoad = OpenSettingsDialog.FileName;
                }
                else
                {
                    return;
                }
                using (StreamReader reader = new StreamReader(FileToLoad))
                {
                    string LoadedJSON = reader.ReadToEnd();
                    ServerSettings LoadedSettings = System.Text.Json.JsonSerializer.Deserialize<ServerSettings>(LoadedJSON);
                    serverSettings = LoadedSettings;
                    DataContext = serverSettings;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async void FileMenuSave_Click(object sender, RoutedEventArgs e)
        {
            if (servers.Count > 0)
            {
                ContentDialog yesNoDialog = new()
                {
                    Content = "是否自动保存到服务器？如果原始文件存在，将创建其备份。",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };

                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
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
                            MessageBox.Show("在服务器路径中未找到SaveData文件夹。请确保您已启动服务器一次。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        if (File.Exists(server.Path + @"\SaveData\Settings\ServerHostSettings.json"))
                            File.Copy(server.Path + @"\SaveData\Settings\ServerHostSettings.json", server.Path + @"\SaveData\Settings\ServerHostSettings.bak", true);

                        string SettingsJSON = System.Text.Json.JsonSerializer.Serialize(serverSettings, serializerOptions);
                        File.WriteAllText(server.Path + @"\SaveData\Settings\ServerHostSettings.json", SettingsJSON);

                        ContentDialog closeFileDialog = new()
                        {
                            Content = "文件成功保存于：\n" + server.Path + @"\SaveData\Settings\ServerHostSettings.json",
                            PrimaryButtonText = "是",
                        };
                        await closeFileDialog.ShowAsync();
                        
                        
                        string jsonString;
                        string jsonFilePath = server.Path + @"\SaveData\Settings\ServerHostSettings.json";
                        using (StreamReader reader = new StreamReader(jsonFilePath))
                        {
                            jsonString = reader.ReadToEnd();
                        }
                        var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonString);
                        if (File.Exists(server.Path + @"\start_server_example.bat"))
                        {
                            string[] StartServerCommond = { "@echo off\n" +
                                "REM Copy this script to your own file and modify to your content. This file can be overwritten when updating.\n " +
                                "set SteamAppId=1604030\n" +
                                "echo \"Starting V Rising Dedicated Server - PRESS CTRL-C to exit\"\n\n" +
                                "@echo on\n" +
                                "VRisingServer.exe -persistentDataPath " + server.Path + @"\SaveData " + "-serverName \"" + jsonObject.Name  + "\" -saveName \"world1\" -logFile \".\\logs\\VRisingServer.log\""
                            };
                            File.WriteAllLines(server.Path + @"\start_server_example.txt", StartServerCommond, System.Text.Encoding.UTF8);

                            if (File.Exists(server.Path + @"\start_server_example.bat"))
                            {
                                File.Delete(server.Path + @"\start_server_example.bat");
                                File.Copy(server.Path + @"\start_server_example.txt", server.Path + @"\start_server_example.bat");
                            }
                                File.Delete(server.Path + @"\start_server_example.txt");
                        }
                        string modifiedJsonString = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                        using (StreamWriter writer = new StreamWriter(jsonFilePath))
                        {
                            writer.Write(modifiedJsonString);
                        }
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            try
            {
                string SettingsJSON = System.Text.Json.JsonSerializer.Serialize(serverSettings, serializerOptions);
                SaveFileDialog SaveSettingsDialog = new SaveFileDialog
                {
                    Filter = "\"JSON files\"|*.json",
                    DefaultExt = "json",
                    FileName = "ServerHostSettings.json",
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
