﻿using ModernWpf.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Security.Cryptography.Pkcs;
using System.Windows;

namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for ManagerSettings.xaml
    /// </summary>
    public partial class ManagerSettings : Window
    {
        MainSettings localMainSettings;
        MainWindow MainWindow = new();
        //public bool managerSettingsClose { get; set; } = false;
        public ManagerSettings(MainSettings mainSettings)
        {
            localMainSettings = mainSettings;
            DataContext = localMainSettings;
            InitializeComponent();

            if (mainSettings.Servers.Count > 0 )
            {
                ServerCombo.SelectedIndex = 0;
                ServerCombo2.SelectedIndex = 0;
            }
            else
            {
                ServerCombo.IsEnabled = false;
                ServerCombo2.IsEnabled = false;
                ResetServerButton.IsEnabled = false;
            }
        }

        private void LogToConsole(string logMessage)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                MainWindow.MainMenuConsole.AppendText(logMessage + "\r");
                MainWindow.MainMenuConsole.ScrollToEnd();
            }));
        }

        private async void ModSupportCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (localMainSettings.AppSettings.EnableModSupport == false)
            {
                return;
            }

            ContentDialog yesNoDialog = new()
            {
                Title = "警告",
                Content =   "对MOD的支持仍处于实验阶段，除非作者的格式与标准不同，否则大多数MOD将自动运行和安装。" +
                                "\n如果您正在安装新的MOD，请确保您安装的MOD在最新版本上运行并定期创建存储的备份。" +
                                    "\n服务端管理器(VSM)不能对MOD中断/损坏您的存储负责。" +
                                        "\n\n是否启用MOD支持？",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                Owner = this
            };

            if (await yesNoDialog.ShowAsync() != ContentDialogResult.Primary)
                localMainSettings.AppSettings.EnableModSupport = false;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            MainSettings.Save(localMainSettings);
            localMainSettings.AppSettings.ManagerSettingsClose = true;
            this.Close();
        }

        private void ResetServerButton_Click(object sender, RoutedEventArgs e)
        {
            localMainSettings.Servers[ServerCombo.SelectedIndex].WebhookMessages = new();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {            
            localMainSettings.WebhookSettings.UpdateFound = new Webhook().UpdateFound;
            localMainSettings.WebhookSettings.UpdateWait = new Webhook().UpdateWait;
        }

    }
}
