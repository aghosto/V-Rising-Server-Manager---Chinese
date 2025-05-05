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
        private JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly ObservableCollection<Server> servers;

        public ChangeSaveFileEditor(ObservableCollection<Server> sentServers, bool autoLoad = false, int indexToLoad = -1)
        {
            servers = sentServers;
            changeServerSaveSettings = new ChangeServerSaveSettings();
            DataContext = changeServerSaveSettings;
            InitializeComponent();
        }
        private void ServerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Server server = (Server)ServerComboBox.SelectedItem;

            if (1== 1)
            {
                for (int i = 0; i < server.InstalledMods.Count; i++)
                {
                    
                }
            }
            else
            {

            }
        }
    }
}
