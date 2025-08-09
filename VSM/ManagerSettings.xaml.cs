using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Media;
using System.Collections.Specialized;
using static VRisingServerManager.LogManager;

namespace VRisingServerManager
{
    /// <summary>
    /// 管理器设置窗口
    /// </summary>
    public partial class ManagerSettings : Window
    {
        // 引用主设置数据（避免复制，直接操作原实例）
        private readonly MainSettings _mainSettings;

        // 主窗口实例（通过应用获取，避免新建）
        private MainWindow _mainWindow;

        public ManagerSettings(MainSettings mainSettings)
        {
            _mainSettings = mainSettings ?? throw new ArgumentNullException(nameof(mainSettings), "主设置数据不能为null");
            InitializeComponent();

            DataContext = _mainSettings; // 绑定到主设置数据
            _mainWindow = Application.Current.MainWindow as MainWindow;
            UpdateServerComboState();
            _mainSettings.Servers.CollectionChanged += Servers_CollectionChanged;
        }

        /// <summary>
        /// 当服务器集合变化时（添加/删除服务器），更新控件状态
        /// </summary>
        private void Servers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateServerComboState();
        }

        /// <summary>
        /// 更新服务器选择下拉框的启用状态和默认选中项
        /// </summary>
        private void UpdateServerComboState()
        {
            bool hasServers = _mainSettings.Servers.Count > 0;

            ServerCombo.IsEnabled = hasServers;
            ServerCombo2.IsEnabled = hasServers;
            ResetServerButton.IsEnabled = hasServers;

            if (hasServers && ServerCombo.SelectedIndex == -1)
            {
                ServerCombo.SelectedIndex = 0;
            }
            if (hasServers && ServerCombo2.SelectedIndex == -1)
            {
                ServerCombo2.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 向主窗口控制台输出日志（带颜色）
        /// </summary>
        private void ShowLogMsg(string logMessage, Brush color)
        {
            if (_mainWindow == null)
            {
                _mainWindow = Application.Current.MainWindow as MainWindow;
                if (_mainWindow == null)
                {
                    return;
                }
            }

            _mainWindow.ShowLogMsg(LogType.MainConsole, logMessage, color);
        }

        /// <summary>
        /// 启用MOD支持的警告对话框
        /// </summary>
        private async void ModSupportCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_mainSettings.AppSettings == null) 
                return;

            if (!_mainSettings.AppSettings.EnableModSupport) 
                return;

            // 显示警告对话框
            var dialog = new ContentDialog
            {
                Title = "警告",
                Content = "对MOD的支持仍处于实验阶段，除非作者的格式与标准不同，否则大多数MOD将自动运行和安装。\n" +
                          "如果您正在安装新的MOD，请确保您安装的MOD在最新版本上运行并定期创建存储的备份。\n" +
                          "服务端管理器(VSM)不能对MOD中断/损坏您的存储负责。\n\n" +
                          "是否启用MOD支持？",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                Owner = this
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                _mainSettings.AppSettings.EnableModSupport = false;
                ShowLogMsg("已取消启用MOD支持", Brushes.Yellow);
            }
            else
            {
                ShowLogMsg("已启用MOD支持（实验阶段）", Brushes.LimeGreen);
            }
        }

        /// <summary>
        /// 保存设置并关闭窗口
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _mainSettings.AppSettings.ManagerSettingsClose = true;
                MainSettings.Save(_mainSettings);
                ShowLogMsg("软件设置已保存", Brushes.LimeGreen);
                Close();
            }
            catch (Exception ex)
            {
                //ShowLogMsg($"软件保存设置失败：{ex.Message}", Brushes.Red);

                _ = new ContentDialog
                {
                    Title = "保存失败",
                    Content = $"保存设置时出错：{ex.Message}",
                    PrimaryButtonText = "确定",
                    Owner = this
                }.ShowAsync();
            }
        }

        /// <summary>
        /// 重置选中服务器的Webhook消息设置
        /// </summary>
        private void ResetServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServerCombo.SelectedIndex < 0 || ServerCombo.SelectedIndex >= _mainSettings.Servers.Count)
            {
                ShowLogMsg("重置Webhook设置失败：未选择有效服务器", Brushes.Red);

                _ = new ContentDialog
                {
                    Title = "操作失败",
                    Content = "请先选择一个有效的服务器",
                    PrimaryButtonText = "确定",
                    Owner = this
                }.ShowAsync();
                return;
            }

            var server = _mainSettings.Servers[ServerCombo.SelectedIndex];
            server.WebhookMessages = new ServerWebhook();
            ShowLogMsg($"已重置服务器 {server.vsmServerName} 的Webhook设置", Brushes.LimeGreen);
        }

        /// <summary>
        /// 重置全局Webhook设置
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultWebhook = new Webhook();
            _mainSettings.WebhookSettings.UpdateFound = defaultWebhook.UpdateFound;
            _mainSettings.WebhookSettings.UpdateWait = defaultWebhook.UpdateWait;
            ShowLogMsg("已重置全局Webhook设置", Brushes.LimeGreen);
        }

        /// <summary>
        /// 窗口关闭时清理事件监听
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _mainSettings.Servers.CollectionChanged -= Servers_CollectionChanged;
        }
    }
}