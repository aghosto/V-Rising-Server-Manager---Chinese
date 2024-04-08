using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Windows.Navigation;

namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class AdminManager : Window
    {
        Server serverToManage;

        public AdminManager(Server server)
        {
            InitializeComponent();
            serverToManage = server;
            ReloadList(serverToManage.Path + @"\SaveData\Settings\adminlist.txt");
            AdminList.SelectionChanged += AdminList_SelectionChanged;
        }

        private void AdminList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (AdminList.SelectedIndex != -1)
            {
                RemoveAdminButton.IsEnabled = true;
            }
            else
            {
                RemoveAdminButton.IsEnabled = false;
            }
        }

        public void ReloadList(string filePath)
        {
            AdminList.Items.Clear();
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    AdminList.Items.Add(line);
                }
                sr.Close();
            }
            if (AdminList.Items.Count > 0)
                AdminList.SelectedIndex = AdminList.Items.Count - 1;
        }

        private void AddAdminButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdminToAdd.Text != "")
            {
                AdminList.Items.Add(AdminToAdd.Text);
                if (AdminList.Items.Count > 0)
                    AdminList.SelectedIndex = AdminList.Items.Count - 1;
            }
            else
            {
                MessageBox.Show("SteamID为空，添加失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAdminButton_Click(object sender, RoutedEventArgs e)
        {
            if (AdminList.SelectedIndex != -1)
            {
                AdminList.Items.RemoveAt(AdminList.SelectedIndex);
                if (AdminList.Items.Count > 0)
                    AdminList.SelectedIndex = AdminList.Items.Count - 1;
            }
            else
            {
                MessageBox.Show("请选择需要移除的ID", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start("explorer.exe", "https://steamid.io/lookup");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(serverToManage.Path + @"\SaveData\Settings\adminlist.txt"))
            {
                string sPath = @$"{serverToManage.Path}\SaveData\Settings\adminlist.txt";
                StreamWriter SaveFile = new StreamWriter(sPath);
                foreach (var item in AdminList.Items)
                {
                    SaveFile.WriteLine(item);
                }
                SaveFile.Close();
                Close();
            }
            else
            {
                MessageBox.Show("未找到管理员文件(adminlist.txt)\n请确认服务器路径设置正确。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadList(serverToManage.Path + @"\SaveData\Settings\adminlist.txt");
        }
    }
}
