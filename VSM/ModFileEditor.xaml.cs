using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace VRisingServerManager
{
    /// <summary>
    /// ModFileEditor.xaml 的交互逻辑
    /// </summary>
    public partial class ModFileEditor : Window
    {
        private JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private readonly ObservableCollection<Server> servers;
        public ModFileEditor(ObservableCollection<Server> sentServers, ModInfo mod)
        {
            servers = sentServers;
            InitializeComponent();

        }
    }
}
