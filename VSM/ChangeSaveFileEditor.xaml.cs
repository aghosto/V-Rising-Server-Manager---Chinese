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
using System.Windows.Controls;

namespace VRisingServerManager
{
    /// <summary>
    /// ChangeSaveFileEditor.xaml 的交互逻辑
    /// </summary>
    public partial class ChangeSaveFileEditor : Window
    {
        private ChangeServerSaveSettings changeServerSaveSettings;
        MainSettings VsmSettings = new();


        public ChangeSaveFileEditor(MainSettings mainSettings)
        {
            VsmSettings = mainSettings;

            ServerComboBox.DataContext = mainSettings;

            if (VsmSettings.Servers.Count > 0)
            {
                ServerComboBox.SelectedIndex = 0;
            }
            changeServerSaveSettings = new ChangeServerSaveSettings();
            DataContext = changeServerSaveSettings;
            InitializeComponent();
        }
        private void ServerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Server server = (Server)ServerComboBox.SelectedItem;

            //if (1== 1)
            //{
            //    for (int i = 0; i < server.InstalledMods.Count; i++)
            //    {
                    
            //    }
            //}
            //else
            //{

            //}
        }

        private void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ChangeSaveFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void UpdateServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RconServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ServerFolderButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ManageAdminsButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RestartServerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ThemeSelect_Click(object sender, RoutedEventArgs e)
        {

        }

        private void VoiceServices_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ServerSpecSettingsEditor_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
