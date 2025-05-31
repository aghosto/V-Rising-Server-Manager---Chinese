using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using VRisingServerManager.RCON;
using System.Security.AccessControl;
using System.Drawing.Printing;

namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for RconConsole.xaml
    /// </summary>
    public partial class RconConsole : Window
    {
        RemoteConClient rClient;
        Server server;
        //Rcon rcon = new Rcon();
        public RconConsole(Server loadedServer)
        {
            server = loadedServer;
            DataContext = server;
            InitializeComponent();
            RconConsoleOutput.AppendText("远程控制(RCON)客户端已就绪。");
            CommandList.SelectedIndex = 0;
            //CommandCombo.SelectedIndex = 0;
        }

        private void LogToConsole(string output)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                RconConsoleOutput.AppendText("\r" + output);
                RconConsoleOutput.ScrollToEnd();
            }));            
        }
        //private class CommandList
        //{
        //    private string Help = "help";
        //    private string Announce = "announce";
        //    private string AnnounceRestart = "announcerestart";
        //    private string Shutdown = "shutdown";
        //    private string CancelShutdown = "cancelshutdown";
        //    private string Version = "version";
        //    private string Time = "time";
        //    private string Name = "name";
        //    private string Description = "description";
        //    private string Password = "password";
        //}

        private void Connect()
        {
            rClient = new()
            {
                UseUtf8 = true
            };
            Dispatcher.Invoke(new Action(() =>
            {
                ConnectButton.IsEnabled = false;
            }));            
            rClient.OnLog += message =>
            {
                LogToConsole(message);
            };
            rClient.OnConnectionStateChange += state =>
            {
                if (state == RemoteConClient.ConnectionStateChange.Connected)
                {
                    rClient.Authenticate(server.RconServerSettings.Password);
                    Dispatcher.Invoke(new Action(() =>
                    {
                        DisconnectButton.IsEnabled = true;
                        ParamaterTextbox.IsEnabled = true;
                        SendCommandButton.IsEnabled = true;                        
                    }));
                }
                if (state == RemoteConClient.ConnectionStateChange.Disconnected)
                {                    
                    Dispatcher.Invoke(new Action(() =>
                    {
                        DisconnectButton.IsEnabled = false;
                        ConnectButton.IsEnabled = true;
                        ParamaterTextbox.IsEnabled = false;
                        SendCommandButton.IsEnabled = false;                        
                    }));
                    LogToConsole("已连接。");
                }
                if (state == RemoteConClient.ConnectionStateChange.ConnectionLost)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        DisconnectButton.IsEnabled = false;
                        ConnectButton.IsEnabled = true;
                        ParamaterTextbox.IsEnabled = false;
                        SendCommandButton.IsEnabled = false;
                    }));
                    LogToConsole("连接丢失。");
                }
                if (state == RemoteConClient.ConnectionStateChange.ConnectionTimeout)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        DisconnectButton.IsEnabled = false;
                        ConnectButton.IsEnabled = true;
                        ParamaterTextbox.IsEnabled = false;
                        SendCommandButton.IsEnabled = false;
                    }));
                    LogToConsole("连接超时。");
                }
                if (state == RemoteConClient.ConnectionStateChange.NoConnection)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        DisconnectButton.IsEnabled = false;
                        ConnectButton.IsEnabled = true;
                        ParamaterTextbox.IsEnabled = false;
                        SendCommandButton.IsEnabled = false;                        
                    }));
                    LogToConsole($"未能连接至 {server.RconServerSettings.IPAddress}:{server.RconServerSettings.Port}。");
                }
            };            
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(Connect);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            await Task.Run(() =>
            {
                rClient.Connect(server.RconServerSettings.IPAddress, int.Parse(server.RconServerSettings.Port));
                {
                    while (!rClient.Connected)
                    {
                        Thread.Sleep(10);
                    }
                }
            });
        }

        private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CommandList.SelectedIndex)
            {
                //case 0:
                //    {
                //        .Text = "查看所有服务器 RCon 命令";
                //    }
                //    break;
                //case 1:
                //    {
                //        CommandInfoTextbox.Text = "向连接到服务器的所有玩家发送消息。\n\n例如：hello。（注意：只能发英文）";
                //    }
                //    break;
                //case 2:
                //    {
                //        CommandInfoTextbox.Text = "向连接到服务器的所有玩家发送更改预设的消息，该消息在x分钟内宣布服务器重新启动。\n不如上述公告灵活，但具有本地化到每种用户语言的优势。\n\n示例参数：5";
                //    }
                //    break;
                default:
                    //    {
                    //        CommandInfoTextbox.Text = "请选择一个指令。";
                    //    }
                    break;
            }
        }
        //private void CommandListCombo_Index(object sender, RoutedEvent e)
        //{
        //    switch (CommandListCombo.SelectedIndex)
        //    {
        //        case 0:
        //            rcon.CommandCurrent = "";
        //            break;
        //        case 1:
        //            rcon.CommandCurrent = "help";
        //            break;
        //        case 2:
        //            rcon.CommandCurrent = "announce";
        //            break;
        //        case 3:
        //            rcon.CommandCurrent = "announcerestart";
        //            break;
        //        case 4:
        //            rcon.CommandCurrent = "shutdown";
        //            break;
        //        case 5:
        //            rcon.CommandCurrent = "cancelshutdown";
        //            break;
        //        case 6:
        //            rcon.CommandCurrent = "version";
        //            break;
        //        case 7:
        //            rcon.CommandCurrent = "time";
        //            break;
        //        case 8:
        //            rcon.CommandCurrent = "name";
        //            break;
        //        case 9:
        //            rcon.CommandCurrent = "description";
        //            break;
        //        case 10:
        //            rcon.CommandCurrent = "password";
        //            break;
        //    }
        //}
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (rClient.Connected)
            {
                rClient.Disconnect();
            }
        }

        private void SendCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (CommandList.SelectedIndex != -1 && rClient.Connected == true)
            {
                rClient.SendCommand(string.Format("{0} {1}", (CommandList.SelectedItem as ListBoxItem).Content.ToString(), ParamaterTextbox.Text), result => { LogToConsole(result); });
                LogToConsole(string.Format("已发送 {0}，参数 {1}。", (CommandList.SelectedItem as ListBoxItem).Content.ToString(), ParamaterTextbox.Text));
            }
            else
            {
                LogToConsole("发送失败\r请确认远程控制是否连接以及是否已选择了指令？");
            }
        }
    }
}
