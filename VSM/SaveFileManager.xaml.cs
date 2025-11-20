using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace VRisingServerManager
{
    public partial class SaveFileManager : Window
    {
        private string _baseSavePath;
        private string _selectedSavePath;
        private Server _selectedServer;
        private List<Server> _servers;

        // 存档节点类型
        public enum SaveNodeType
        {
            User,       // Steam用户节点
            Version,    // 版本节点(v3/v4)
            SaveFolder, // 存档文件夹
            SaveFile    // 存档文件
        }

        public class SaveNode
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public SaveNodeType Type { get; set; }
            public string ExtraInfo { get; set; }
            public List<SaveNode> Children { get; set; } = new List<SaveNode>();
        }

        public SaveFileManager(List<Server> servers)
        {
            InitializeComponent();

            // 初始化服务器列表
            _servers = servers;
            InitializeServerList();

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _baseSavePath = Path.Combine(userProfile, "AppData", "LocalLow", "Stunlock Studios", "VRising", "CloudSaves");

            // 加载存档列表
            LoadSaveFiles();

            // 注册转换器
            Resources.Add("ServerStateToColorConverter", new ServerStateToColorConverter());
        }

        private void InitializeServerList()
        {
            if (_servers == null)
            {
                _servers = new List<Server>();
                SelectedServerInfo.Text = "服务器列表为空";
                return;
            }
            if (_servers != null && _servers.Any())
            {
                ServersListBox.ItemsSource = _servers;
            }
            else
            {
                SelectedServerInfo.Text = "未找到任何服务器配置";
            }
        }

        private void LoadSaveFiles()
        {
            try
            {
                SavesTreeView.Items.Clear();

                if (!Directory.Exists(_baseSavePath))
                {
                    SelectedSaveInfo.Text = $"未找到存档目录: {_baseSavePath}";
                    return;
                }

                // 获取所有Steam用户文件夹(64位ID)
                var steamUserFolders = Directory.GetDirectories(_baseSavePath)
                    .Where(d => IsValidSteamId(Path.GetFileName(d)))
                    .ToList();

                foreach (var userFolder in steamUserFolders)
                {
                    string steamId = Path.GetFileName(userFolder);
                    var userNode = new SaveNode
                    {
                        Name = $"Steam用户",
                        Path = userFolder,
                        Type = SaveNodeType.User,
                        ExtraInfo = steamId
                    };

                    var versionFolders = Directory.GetDirectories(userFolder)
                        .Where(d =>
                        {
                            string folderName = Path.GetFileName(d);
                            return folderName.Equals("v3", StringComparison.OrdinalIgnoreCase) ||
                                   folderName.Equals("v4", StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();

                    foreach (var versionFolder in versionFolders)
                    {
                        string version = Path.GetFileName(versionFolder);
                        string versionDesc = version == "v3" ? "1.0版本" : "1.1版本";

                        var versionNode = new SaveNode
                        {
                            Name = version,
                            Path = versionFolder,
                            Type = SaveNodeType.Version,
                            ExtraInfo = versionDesc
                        };

                        // 获取编码命名的存档文件夹
                        var saveFolders = Directory.GetDirectories(versionFolder);
                        foreach (var saveFolder in saveFolders)
                        {
                            string folderName = Path.GetFileName(saveFolder);
                            var saveFolderNode = new SaveNode
                            {
                                Name = "存档文件夹",
                                Path = saveFolder,
                                Type = SaveNodeType.SaveFolder,
                                ExtraInfo = folderName
                            };

                            // 获取存档文件
                            var saveFiles = Directory.GetFiles(saveFolder, "*.save")
                                .OrderBy(f => f)
                                .ToList();

                            foreach (var saveFile in saveFiles)
                            {
                                saveFolderNode.Children.Add(new SaveNode
                                {
                                    Name = Path.GetFileName(saveFile),
                                    Path = saveFile,
                                    Type = SaveNodeType.SaveFile
                                });
                            }

                            // 只添加有存档文件的文件夹
                            if (saveFolderNode.Children.Count > 0)
                            {
                                versionNode.Children.Add(saveFolderNode);
                            }
                        }

                        // 只添加有存档的版本节点
                        if (versionNode.Children.Count > 0)
                        {
                            userNode.Children.Add(versionNode);
                        }
                    }

                    // 只添加有存档的用户节点
                    if (userNode.Children.Count > 0)
                    {
                        SavesTreeView.Items.Add(userNode);
                    }
                }

                // 应用版本筛选
                ApplyVersionFilter();
            }
            catch (Exception ex)
            {
                SelectedSaveInfo.Text = $"加载存档失败: {ex.Message}";
            }
        }

        private void ApplyVersionFilter()
        {
            // 实际应用中应该使用CollectionViewSource和Filter实现
            // 这里简化处理，重新加载存档列表
            LoadSaveFiles();
        }

        private bool IsValidSteamId(string folderName)
        {
            return ulong.TryParse(folderName, out _);
        }

        private void RefreshSaves_Click(object sender, RoutedEventArgs e)
        {
            LoadSaveFiles();
        }

        private void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            InitializeServerList();
        }

        private void SavesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is SaveNode selectedNode)
            {
                bool isSaveItem = selectedNode.Type == SaveNodeType.SaveFile || selectedNode.Type == SaveNodeType.SaveFolder;

                _selectedSavePath = isSaveItem ? selectedNode.Path : null;

                // 更新选中信息
                if (selectedNode.Type == SaveNodeType.SaveFile)
                {
                    SelectedSaveInfo.Text = $"文件: {selectedNode.Path}";
                }
                else if (selectedNode.Type == SaveNodeType.SaveFolder)
                {
                    int fileCount = Directory.GetFiles(selectedNode.Path, "*.save").Length;
                    SelectedSaveInfo.Text = $"文件夹: {selectedNode.Path} (包含 {fileCount} 个存档文件)";
                }
                else
                {
                    SelectedSaveInfo.Text = $"请选择具体的存档文件或文件夹";
                }
            }
            else
            {
                _selectedSavePath = null;
                SelectedSaveInfo.Text = "未选择任何存档";
            }

            UpdateApplyButtonState();
        }

        private void ServersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServersListBox.SelectedItem is Server selectedServer)
            {
                _selectedServer = selectedServer;
                SelectedServerInfo.Text = $"服务器名称: {selectedServer.vsmServerName}\n" +
                                        $"状态: {selectedServer.Runtime.State}\n" +
                                        $"存档路径: {GetServerSavePath(selectedServer)}";
            }
            else
            {
                _selectedServer = null;
                SelectedServerInfo.Text = "未选择任何服务器";
            }

            UpdateApplyButtonState();
        }

        private void UpdateApplyButtonState()
        {
            UseSaveButton.IsEnabled = !string.IsNullOrEmpty(_selectedSavePath) && _selectedServer != null;
        }

        private void UseSelectedSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedSavePath) || _selectedServer == null)
            {
                MessageBox.Show("请先选择存档和目标服务器", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取服务器存档路径
            string serverSavePath = GetServerSavePath(_selectedServer);
            if (string.IsNullOrEmpty(serverSavePath))
            {
                MessageBox.Show("无法获取服务器存档路径", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 确认是否使用选中的存档
            var result = MessageBox.Show(
                $"确定要将以下存档应用到服务器吗?\n\n" +
                $"存档: {_selectedSavePath}\n\n" +
                $"目标服务器: {_selectedServer.vsmServerName}\n" +
                $"服务器存档路径: {serverSavePath}\n\n" +
                $"这将替换当前服务器的存档文件。",
                "确认更换存档",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 执行存档更换逻辑
                    bool success = ReplaceServerSave(_selectedSavePath, serverSavePath);

                    if (success)
                    {
                        MessageBox.Show($"存档已成功应用到服务器 {_selectedServer.vsmServerName}！",
                                      "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("存档更换失败，请检查日志获取详细信息", "失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更换存档时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 替换服务器存档
        /// </summary>
        private bool ReplaceServerSave(string sourcePath, string targetPath)
        {
            try
            {
                // 确保目标目录存在
                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // 创建备份
                CreateSaveBackup(targetPath);

                // 复制新存档
                if (Directory.Exists(sourcePath))
                {
                    // 如果是文件夹，复制所有.save.gz文件
                    foreach (var file in Directory.GetFiles(sourcePath, "*.save.gz"))
                    {
                        string destFile = Path.Combine(targetPath, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }
                else if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".save.gz", StringComparison.OrdinalIgnoreCase))
                {
                    // 如果是单个文件，直接复制
                    string destFile = Path.Combine(targetPath, Path.GetFileName(sourcePath));
                    File.Copy(sourcePath, destFile, true);
                }
                else
                {
                    throw new Exception("选中的路径不是有效的存档文件或文件夹");
                }

                return true;
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Console.WriteLine($"替换存档失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取服务器存档路径
        /// </summary>
        private string GetServerSavePath(Server server)
        {
            // 根据实际情况实现服务器存档路径的获取逻辑
            return Path.Combine(server.Path, "SaveData");
        }

        /// <summary>
        /// 创建存档备份
        /// </summary>
        private void CreateSaveBackup(string savePath)
        {
            try
            {
                string backupPath = Path.Combine(savePath, $"backup_{DateTime.Now:yyyyMMddHHmmss}");
                Directory.CreateDirectory(backupPath);

                foreach (var file in Directory.GetFiles(savePath, "*.save"))
                {
                    File.Copy(file, Path.Combine(backupPath, Path.GetFileName(file)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建存档备份失败: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DragDrop_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragStatusText.Text = "松开鼠标以导入存档";
                DragStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                DragStatusText.Text = "不支持的文件类型";
                DragStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void DragDrop_DragLeave(object sender, DragEventArgs e)
        {
            DragStatusText.Text = "拖拽.save.gz文件或存档文件夹到此处";
            DragStatusText.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void DragDrop_Drop(object sender, DragEventArgs e)
        {
            DragStatusText.Text = "拖拽.save.gz文件或存档文件夹到此处";
            DragStatusText.Foreground = System.Windows.Media.Brushes.Gray;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    string droppedPath = files[0];
                    ProcessDroppedFile(droppedPath);
                }
            }
        }

        /// <summary>
        /// 处理拖拽的文件
        /// </summary>
        private void ProcessDroppedFile(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // 检查是否是有效的存档文件夹
                    int saveFileCount = Directory.GetFiles(path, "*.save.gz").Length;

                    if (saveFileCount > 0)
                    {
                        _selectedSavePath = path;
                        SelectedSaveInfo.Text = $"文件夹: {path} (包含 {saveFileCount} 个存档文件)";
                        UpdateApplyButtonState();
                    }
                    else
                    {
                        SelectedSaveInfo.Text = "选中的文件夹中未找到存档文件";
                    }
                }
                else if (File.Exists(path) && Path.GetExtension(path).Equals(".save.gz", StringComparison.OrdinalIgnoreCase))
                {
                    // 单个存档文件
                    _selectedSavePath = path;
                    SelectedSaveInfo.Text = $"文件: {path}";
                    UpdateApplyButtonState();
                }
                else
                {
                    SelectedSaveInfo.Text = "请拖拽有效的存档文件(.save)或包含存档的文件夹";
                }
            }
            catch (Exception ex)
            {
                SelectedSaveInfo.Text = $"处理文件时出错: {ex.Message}";
            }
        }

        /// <summary>
        /// 浏览文件按钮
        /// </summary>
        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "存档文件 (*.save.gz)|*.save.gz|所有文件 (*.*)|*.*",
                Title = "选择存档文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedSavePath = openFileDialog.FileName;
                SelectedSaveInfo.Text = $"文件: {_selectedSavePath}";
                UpdateApplyButtonState();
            }
        }

        /// <summary>
        /// 浏览文件夹按钮
        /// </summary>
        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择存档文件夹"
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedSavePath = folderDialog.SelectedPath;
                int saveFileCount = Directory.GetFiles(_selectedSavePath, "*.save.gz").Length;
                SelectedSaveInfo.Text = $"文件夹: {_selectedSavePath} (包含 {saveFileCount} 个存档文件)";
                UpdateApplyButtonState();
            }
        }

        /// <summary>
        /// 版本筛选变更事件
        /// </summary>
        private void VersionFilter_Checked(object sender, RoutedEventArgs e)
        {
            ApplyVersionFilter();
        }
    }

    /// <summary>
    /// 服务器状态到颜色的转换器
    /// </summary>
    public class ServerStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string state)
            {
                switch (state)
                {
                    case "运行中":
                        return System.Windows.Media.Brushes.Green;
                    case "已停止":
                        return System.Windows.Media.Brushes.Gray;
                    case "更新中":
                        return System.Windows.Media.Brushes.Orange;
                    case "错误":
                        return System.Windows.Media.Brushes.Red;
                    default:
                        return System.Windows.Media.Brushes.Black;
                }
            }
            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
