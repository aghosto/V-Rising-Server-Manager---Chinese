using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.ComponentModel;
using System.Text.Json.Nodes;
using VRisingServerManager.RCON;
using ModernWpf.Controls;
using ModernWpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Windows.Markup;
using System.Text;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;
using Microsoft.Win32;



namespace VRisingServerManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainSettings VsmSettings = new();
        private static dWebhook DiscordSender = new();
        private static HttpClient HttpClient = new();
        private VoiceServicesSettings VoiceServicesSettings = new();
        private PeriodicTimer? AutoUpdateTimer;
        private RemoteConClient RCONClient;

        public MainWindow()
        {
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\VSMSettings.json"))
                MainSettings.Save(VsmSettings);
            else
                VsmSettings = MainSettings.Load();

            DataContext = VsmSettings;

            if (VsmSettings.AppSettings.DarkMode == true)
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            else
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

            InitializeComponent();

            VsmSettings.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
            VsmSettings.Servers.CollectionChanged += Servers_CollectionChanged; // MVVM method not working

            VsmSettings.AppSettings.Version = new AppSettings().Version;

            LogToConsole("夜族崛起服务端管理器(VSM)启动成功。" + ((VsmSettings.Servers.Count > 0) ? "\r" + VsmSettings.Servers.Count.ToString() + " 个服务器从设置中加载成功。" : "\r未找到服务器，请点击“添加服务器”以开始使用。"));

            ScanForServers();
            SetupTimer();

            if (VsmSettings.AppSettings.AutoUpdateApp == true)
                LookForUpdate();
        }

        private async void LookForUpdate()
        {
            string latestVersion = await HttpClient.GetStringAsync("https://gitee.com/aGHOSToZero/V-Rising-Server-Manager---Chinese/raw/master/VERSION");
            latestVersion = latestVersion.Trim();

            if (latestVersion != VsmSettings.AppSettings.Version)
            {
                ContentDialog yesNoDialog = new()
                {
                    Content = $"软件有新版本可用于下载，需要更新吗？\r\r当前版本：{VsmSettings.AppSettings.Version}\r最新版本：{latestVersion}",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };
                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    Process.Start("VSMUpdater.exe");
                    Application.Current.MainWindow.Close();
                }
                else
                    LogToConsole("用户取消了本次软件更新。");
            }
            else
            {
                LogToConsole($"正在运行最新的版本：{latestVersion}");
            }
        }

        /// <summary>
        /// Sets up the timer for AutoUpdates
        /// </summary>
        private void SetupTimer()
        {
            if (VsmSettings.AppSettings.AutoUpdate == true)
            {
#if DEBUG
                //                AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
//                AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
                //                AutoUpdateLoop();
            }
        }

        private async void AutoUpdateLoop()
        {
            while (await AutoUpdateTimer.WaitForNextTickAsync())
            {
                bool foundUpdate = await CheckForUpdate();
                if (foundUpdate == true && VsmSettings.Servers.Count > 0)
                    AutoUpdate();
            }
        }

        private void LogToConsole(string logMessage)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                MainMenuConsole.AppendText(logMessage + "\r");
                MainMenuConsole.ScrollToEnd();
            }));
        }

        private void SendDiscordMessage(string message)
        {
            if (VsmSettings.WebhookSettings.Enabled == false || message == "")
                return;

            if (VsmSettings.WebhookSettings.URL == "")
            {
                LogToConsole("Discord webhook尝试发送消息，但URL未定义。");
                return;
            }

            if (DiscordSender.WebHook == null)
            {
                DiscordSender.WebHook = VsmSettings.WebhookSettings.URL;
            }

            DiscordSender.SendMessage(message);
        }

        /// <summary>
        /// Updates SteamCMD, used when the executable could not be found
        /// </summary>
        /// <returns><see cref="bool"/> true if succeeded</returns>
        private async Task<bool> UpdateSteamCMD()
        {
            string workingDir = Directory.GetCurrentDirectory();
            LogToConsole("未找到SteamCMD，正在下载...");
            byte[] fileBytes = await HttpClient.GetByteArrayAsync(@"https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
            await File.WriteAllBytesAsync(workingDir + @"\steamcmd.zip", fileBytes);
            if (File.Exists(workingDir + @"\SteamCMD\steamcmd.exe") == true)
            {
                File.Delete(workingDir + @"\SteamCMD\steamcmd.exe");
            }
            LogToConsole("解压中...");
            ZipFile.ExtractToDirectory(workingDir + @"\steamcmd.zip", workingDir + @"\SteamCMD");
            if (File.Exists(workingDir + @"\steamcmd.zip"))
            {
                File.Delete(workingDir + @"\steamcmd.zip");
            }

            LogToConsole("正在获取V Rising应用信息。");
            await CheckForUpdate();

            return true;
        }

        private async Task<bool> UpdateGame(Server server)
        {
            server.Runtime.State = ServerRuntime.ServerState.Updating;

            if (server.Runtime.Process != null)
            {
                LogToConsole("服务器已经启动并存活。");
                return false;
            }

            if (!File.Exists(Directory.GetCurrentDirectory() + @"\SteamCMD\steamcmd.exe"))
            {
                bool sCMDSuccess = await UpdateSteamCMD();
                if (!sCMDSuccess == true)
                {
                    LogToConsole("下载SteamCMD失败，更新程序正在运行。");
                    server.Runtime.State = ServerRuntime.ServerState.Stopped;
                    return false;
                }
            }

            string workingDir = Directory.GetCurrentDirectory();
            LogToConsole("正在更新游戏服务器：" + server.vsmServerName + "，在完成前请勿关闭窗口或对软件进行其他操作。");
            LogToConsole("若显示更新成功但点击启动失败请到软件设置中把 “显示SteamCMD窗口” 选项打开");
            string[] installScript = { "force_install_dir \"" + server.Path + "\"", "login anonymous", (VsmSettings.AppSettings.VerifyUpdates) ? "app_update 1829350 validate" : "app_update 1829350", "quit" };
            if (File.Exists(server.Path + @"\steamcmd.txt"))
                File.Delete(server.Path + @"\steamcmd.txt");
            File.WriteAllLines(server.Path + @"\steamcmd.txt", installScript);
            string parameters = $@"+runscript ""{server.Path}\steamcmd.txt""";

            Process steamcmd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = workingDir + @"\SteamCMD\steamcmd.exe",
                    Arguments = parameters,
                    CreateNoWindow = !VsmSettings.AppSettings.ShowSteamWindow
                }
            };

            steamcmd.Start();
            await steamcmd.WaitForExitAsync();

            LogToConsole("更新游戏服务器成功：" + server.vsmServerName);
            server.Runtime.State = ServerRuntime.ServerState.Stopped;


            return true;
        }

        private async Task<bool> StartServer(Server server)
        {
            if (server.Runtime.Process != null)
            {
                LogToConsole($"错误：{server.vsmServerName} 已在运行中");
                return false;
            }
            if (!Directory.Exists(server.Path + @"\SaveData\Settings"))
            {
                Directory.CreateDirectory(server.Path + @"\SaveData\Settings");
                File.Copy(server.Path + @"\VRisingServer_Data\StreamingAssets\Settings\ServerHostSettings.json", server.Path + @"\SaveData\Settings\ServerHostSettings.json");
                File.Copy(server.Path + @"\VRisingServer_Data\StreamingAssets\Settings\ServerGameSettings.json", server.Path + @"\SaveData\Settings\ServerGameSettings.json");
            }

            await Task.Delay(3000);
            string jsonString;
            string jsonFilePath = server.Path + @"\SaveData\Settings\ServerHostSettings.json";
            using (StreamReader reader = new StreamReader(jsonFilePath))
            {
                jsonString = reader.ReadToEnd();
            }
            var jsonObject = JsonConvert.DeserializeObject<dynamic>(jsonString);

            if (File.Exists(server.Path + @"\VRisingServer.exe"))
            {
                LogToConsole("启动服务器：" + server.vsmServerName + (server.Runtime.RestartAttempts > 0 ? $" 尝试 {server.Runtime.RestartAttempts}/3." : ""));
                if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.StartServer) && server.WebhookMessages.Enabled == true)
                    SendDiscordMessage(server.WebhookMessages.StartServer);
                string parameters = $@"-persistentDataPath ""{server.Path + @"\SaveData"}"" -serverName ""{jsonObject.Name}"" -saveName ""{server.LaunchSettings.WorldName}"" -logFile ""{server.Path + @"\logs\VRisingServer.log"}""{(server.LaunchSettings.BindToIP ? $@" -address ""{server.LaunchSettings.BindingIP}""" : "")}";
                Process serverProcess = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Minimized,
                        FileName = server.Path + @"\VRisingServer.exe",
                        UseShellExecute = true,
                        Arguments = parameters
                    },
                    EnableRaisingEvents = true
                };
                serverProcess.Exited += new EventHandler((sender, e) => ServerProcessExited(sender, e, server));
                serverProcess.Start();
                server.Runtime.State = ServerRuntime.ServerState.Running;
                server.Runtime.UserStopped = false;
                server.Runtime.Process = serverProcess;

                return true;
            }

            else
            {
                LogToConsole("服务器启动程序(VRisingServer.exe)未找到，请确认服务器已正确安装。");
                return false;
            }
        }

        private async Task SendRconRestartMessage(Server server)
        {
            RCONClient = new()
            {
                UseUtf8 = true
            };

            RCONClient.OnLog += async message =>
            {
                if (message == "Authentication success.")
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    RCONClient.SendCommand("announcerestart 5", result =>
                    {
                        //Do nothing
                    });
                }

            };

            RCONClient.OnConnectionStateChange += state =>
            {
                if (state == RemoteConClient.ConnectionStateChange.Connected)
                {
                    RCONClient.Authenticate(server.RconServerSettings.Password);
                }
            };

            RCONClient.Connect(server.RconServerSettings.IPAddress, int.Parse(server.RconServerSettings.Port));
            await Task.Delay(TimeSpan.FromSeconds(3));
            RCONClient.Disconnect();
        }

        private void ScanForServers()
        {
            int foundServers = 0;

            Process[] serverProcesses = Process.GetProcessesByName("vrisingserver");
            foreach (Process process in serverProcesses)
            {
                foreach (Server server in VsmSettings.Servers)
                {
                    if (process.MainModule.FileName == server.Path + @"\VRisingServer.exe")
                    {
                        server.Runtime.State = ServerRuntime.ServerState.Running;
                        process.EnableRaisingEvents = true;
                        process.Exited += new EventHandler((sender, e) => ServerProcessExited(sender, e, server));
                        server.Runtime.Process = process;
                        foundServers++;
                    }
                }
            }

            foreach (Server server in VsmSettings.Servers)
            {
                if (server.AutoStart == true && server.Runtime.State == ServerRuntime.ServerState.Stopped)
                {
                    StartServer(server);
                }
            }

            if (foundServers > 0)
            {
                LogToConsole($"已找到 {foundServers} 个服务器正在运行。");
            }
        }

        private async void AutoUpdate()
        {
            SendDiscordMessage(VsmSettings.WebhookSettings.UpdateFound);

            if (!File.Exists(Directory.GetCurrentDirectory() + @"\SteamCMD\steamcmd.exe"))
            {
                await UpdateSteamCMD();
            }

            List<Task> serverTasks = new List<Task>();
            List<Server> runningServers = new List<Server>();

            foreach (Server server in VsmSettings.Servers)
            {
                if (server.Runtime.State == ServerRuntime.ServerState.Running)
                {
                    runningServers.Add(server);
                }
            }

            foreach (Server server in runningServers)
            {
                if (server.RconServerSettings.Enabled == true)
                {
                    await SendRconRestartMessage(server);
                }
            }

            if (VsmSettings.WebhookSettings.Enabled == true && VsmSettings.WebhookSettings.URL != "" && runningServers.Count > 0)
            {
                SendDiscordMessage(VsmSettings.WebhookSettings.UpdateWait);
#if DEBUG
                await Task.Delay(TimeSpan.FromSeconds(10));
#else
                await Task.Delay(TimeSpan.FromMinutes(5));
#endif
            }

            foreach (Server server in runningServers)
            {
                serverTasks.Add(StopServer(server));
            }

            LogToConsole($"正在自动更新 {VsmSettings.Servers.Count} 个服务器。" + ((runningServers.Count > 0) ? $"\r在此之前即将关闭 {runningServers.Count} 个服务器。" : ""));

            await Task.WhenAll(serverTasks.ToArray());
            serverTasks.Clear();

            foreach (Server server in VsmSettings.Servers)
            {
                await UpdateGame(server);
            }

            foreach (Server server in runningServers)
            {
                serverTasks.Add(StartServer(server));
            }

            await Task.WhenAll(serverTasks.ToArray());
            LogToConsole("自动更新完成。");
        }

        private async Task<bool> StopServer(Server server)
        {
            if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.StopServer) && server.WebhookMessages.Enabled == true)
                SendDiscordMessage(server.WebhookMessages.StopServer);

            server.Runtime.UserStopped = true;

            bool success;
            bool close = server.Runtime.Process.CloseMainWindow();

            if (close)
            {
                await server.Runtime.Process.WaitForExitAsync();
                server.Runtime.Process = null;
                success = true;
            }
            else
            {
                success = false;
            }
            return success;
        }

        private async Task<bool> RemoveServer(Server server)
        {
            int serverIndex = VsmSettings.Servers.IndexOf(server);
            string workingDir = Directory.GetCurrentDirectory();
            string serverName = server.vsmServerName.Replace(" ", "_");

            bool success;
            ContentDialog yesNoDialog = new()
            {
                Content = $"确认要移除服务器 {server.vsmServerName}？\n此动作将永久移除该服务器及其文件。",
                PrimaryButtonText = "是",
                SecondaryButtonText = "否"
            };
            if (await yesNoDialog.ShowAsync() is ContentDialogResult.Secondary)
                return false;

            //if (MessageBox.Show($"确认要移除服务器 {server.vsmServerName}？\n此动作将永久移除该服务器及其文件。", "移除服务器—确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            //{
            //    return false;
            //}

            if (serverIndex != -1)
            {
                ContentDialog bakDialog = new()
                {
                    Content = $@"是否为该服务器数据创建备份？{Environment.NewLine}备份将保存于：{workingDir}\Backups\{serverName}_Bak.zip",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };
                if (await bakDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    if (!Directory.Exists(workingDir + @"\Backups"))
                        Directory.CreateDirectory(workingDir + @"\Backups");

                    if (Directory.Exists(server.Path + @"\SaveData\"))
                    {
                        if (File.Exists(workingDir + @"\Backups\" + serverName + "_Bak.zip"))
                            File.Delete(workingDir + @"\Backups\" + serverName + "_Bak.zip");

                        ZipFile.CreateFromDirectory(server.Path + @"\SaveData\", workingDir + @"\Backups\" + serverName + "_Bak.zip");
                    }
                }

                //if (MessageBox.Show($@"是否为该服务器数据创建备份？{Environment.NewLine}备份将保存于：{workingDir}\Backups\{serverName}_Bak.zip", "移除服务器—备份", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                //{
                //    if (!Directory.Exists(workingDir + @"\Backups"))
                //        Directory.CreateDirectory(workingDir + @"\Backups");

                //    if (Directory.Exists(server.Path + @"\SaveData\"))
                //    {
                //        if (File.Exists(workingDir + @"\Backups\" + serverName + "_Bak.zip"))
                //            File.Delete(workingDir + @"\Backups\" + serverName + "_Bak.zip");

                //        ZipFile.CreateFromDirectory(server.Path + @"\SaveData\", workingDir + @"\Backups\" + serverName + "_Bak.zip");
                //    }
                //}
                VsmSettings.Servers.RemoveAt(serverIndex);
                if (Directory.Exists(server.Path))
                    Directory.Delete(server.Path, true);
                success = true;
                return success;
            }
            else
            {
                return false;
            }
        }

        private async Task<bool> CheckForUpdate()
        {
            bool foundUpdate = false;
            LogToConsole("正在查询更新...");
            string json = await HttpClient.GetStringAsync("https://api.steamcmd.net/v1/info/1829350");
            JsonNode jsonNode = JsonNode.Parse(json);

            var version = jsonNode!["data"]["1829350"]["depots"]["branches"]["public"]["timeupdated"]!.ToString();

            if (version == VsmSettings.AppSettings.LastUpdateTimeUNIX)
            {
                VsmSettings.AppSettings.LastUpdateTimeUNIX = version;
                foundUpdate = false;
                if (VsmSettings.AppSettings.LastUpdateTimeUNIX != "")
                    VsmSettings.AppSettings.LastUpdateTime = "服务端最近更新的时间：" + DateTimeOffset.FromUnixTimeSeconds(long.Parse(VsmSettings.AppSettings.LastUpdateTimeUNIX)).DateTime.ToString();

                MainSettings.Save(VsmSettings);
                LogToConsole("当前游戏服务器已是最新版本。");
                return foundUpdate;
            }

            if (version != VsmSettings.AppSettings.LastUpdateTimeUNIX)
            {
                VsmSettings.AppSettings.LastUpdateTimeUNIX = version;
                foundUpdate = true;
            }

            if (VsmSettings.AppSettings.LastUpdateTimeUNIX == "")
            {
                VsmSettings.AppSettings.LastUpdateTimeUNIX = version;
                foundUpdate = true;
            }

            if (VsmSettings.AppSettings.LastUpdateTimeUNIX != "")
                VsmSettings.AppSettings.LastUpdateTime = "上一次在Steam更新的时间：" + DateTimeOffset.FromUnixTimeSeconds(long.Parse(VsmSettings.AppSettings.LastUpdateTimeUNIX)).DateTime.ToString();

            MainSettings.Save(VsmSettings);
            return foundUpdate;
        }

        private async void ReadLog(Server server)
        {
            using (FileStream fs = new FileStream(server.Path + @"\Logs\VRisingServer.log", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs))
            {
                string ipAddress = "";
                string steamID = "";
                int foundVariables = 0;

                while (foundVariables < 3 && server.Runtime.Process != null)
                {
                    string line = await sr.ReadLineAsync();
                    if (line != null)
                    {
                        if (line.Contains("SteamPlatformSystem - OnPolicyResponse - Public IP: "))
                        {
                            ipAddress = line.Split("SteamPlatformSystem - OnPolicyResponse - Public IP: ")[1];
                            foundVariables++;
                        }
                        if (line.Contains("SteamNetworking - Successfully logged in with the SteamGameServer API. SteamID: "))
                        {
                            steamID = line.Split("SteamNetworking - Successfully logged in with the SteamGameServer API. SteamID: ")[1];
                            foundVariables++;
                        }
                        if (line.Contains("Shutting down Asynchronous Streaming"))
                            foundVariables++;
                    }
                }

                if (foundVariables == 3 && VsmSettings.WebhookSettings.Enabled == true && server.WebhookMessages.Enabled == true)
                {
                    List<string> toSendList = new()
                    {
                        !string.IsNullOrEmpty(server.WebhookMessages.ServerReady) ? server.WebhookMessages.ServerReady : "",
                        (server.WebhookMessages.BroadcastIP == true) ? $"Public IP: {ipAddress}" : "",
                        (server.WebhookMessages.BroadcastSteamID == true) ? $"SteamID: {steamID}" : ""
                    };

                    if (!toSendList.All(x => string.IsNullOrEmpty(x)))
                    {
                        string toSend = string.Join("\r", toSendList);
                        SendDiscordMessage(toSend);
                    }
                }

                sr.Close();
                fs.Close();
            }
        }

        #region Events
        private async void ServerProcessExited(object sender, EventArgs e, Server server)
        {
            server.Runtime.State = ServerRuntime.ServerState.Stopped;

            switch (server.Runtime.Process.ExitCode)
            {
                case 1:
                    LogToConsole($"{server.vsmServerName} 崩溃了。");
                    break;
                case -2147483645:
                    LogToConsole($"{server.vsmServerName} 已中断，代码为‘-2147483645’，端口无法打开时可能会发生这种情况。确保没有其他服务器正在使用相同的端口。");
                    break;
            }

            server.Runtime.Process = null;

            if (server.Runtime.RestartAttempts >= 3)
            {
                LogToConsole($"服务器 '{server.vsmServerName}' 已尝试重新启动3次未成功，正在禁用自动重启功能。");
                if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.AttemptStart3) && server.WebhookMessages.Enabled == true)
                    SendDiscordMessage(server.WebhookMessages.AttemptStart3);
                server.Runtime.RestartAttempts = 0;
                server.AutoRestart = false;
                return;
            }

            if (server.AutoRestart == true && server.Runtime.UserStopped == false)
            {
                server.Runtime.RestartAttempts++;
                if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.ServerCrash) && server.WebhookMessages.Enabled == true)
                    SendDiscordMessage(server.WebhookMessages.ServerCrash);
                await StartServer(server);
            }
        }

        private void AppSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "AutoUpdate":
                    if (VsmSettings.AppSettings.AutoUpdate == true)
                    {
#if DEBUG
                        //                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
//                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
                        //                        AutoUpdateLoop();
                        LookForUpdate();
                    }
                    else
                    {
                        if (AutoUpdateTimer != null)
                        {
                            AutoUpdateTimer.Dispose();
                        }
                    }
                    break;
                case "AutoUpdateInterval":
                    if (VsmSettings.AppSettings.AutoUpdate == true && AutoUpdateTimer != null)
                    {
                        //                        AutoUpdateTimer.Dispose();
#if DEBUG
                        //                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
//                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
                        //                        AutoUpdateLoop();
                    }
                    break;
                case "DarkMode":
                    if (VsmSettings.AppSettings.DarkMode == true)
                    {
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                    }
                    else
                    {
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                    }
                    break;
            }
        }

        private void Servers_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            int serversLength = ServerTabControl.Items.Count;
            if (serversLength > 0)
            {
                ServerTabControl.SelectedIndex = serversLength - 1;
            }
        }
        #endregion

        #region Buttons
        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            bool started = false;
            Server server = ((Button)sender).DataContext as Server;
            MainSettings.Save(VsmSettings);
            if (File.Exists(server.Path + @"\start_server_example.bat"))
                started = await StartServer(server);
            else

                await Task.Delay(3000);

            if (started == true && VsmSettings.WebhookSettings.Enabled)
                ReadLog(server);
        }

        private async void UpdateServerButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;

            await UpdateGame(server);
        }

        private async void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;
            LogToConsole("正在停止服务器：" + server.vsmServerName);
            bool success = await StopServer(server);
            if (success)
            {
                LogToConsole("已成功停止服务器：" + server.vsmServerName);
            }
            else
            {
                LogToConsole("无法停止服务器：" + server.vsmServerName);
            }
        }
        private async void RestartServerButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;
            LogToConsole("正在重启服务器：" + server.vsmServerName + "......");
            bool success = await StopServer(server);

            if (success)
            {
                LogToConsole("已成功停止服务器：" + server.vsmServerName);

            }
            else
            {
                LogToConsole("无法停止服务器：" + server.vsmServerName);
                return;
            }

            bool started = false;
            MainSettings.Save(VsmSettings);
            if (File.Exists(server.Path + @"\start_server_example.bat"))
                started = await StartServer(server);
            else
                await Task.Delay(3000);

            if (started == true && VsmSettings.WebhookSettings.Enabled)
                ReadLog(server);
        }

        private void ThemeSelect_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Light)
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            else
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
        }

        private async void VoiceServices_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;
             
            if (!File.Exists(server.Path + @"\SaveData\Settings\ServerVoipSettings.json"))
            {
                ContentDialog yesNoDialog = new ContentDialog()
                {
                    Content = "未检测到Unity语音配置文件，是否进行配置？",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };
                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    string Json = JsonConvert.SerializeObject(VoiceServicesSettings, Formatting.Indented);
                    File.WriteAllText(server.Path + @"\SaveData\Settings\ServerVoipSettings.json", Json);
                }
                else
                {
                    ContentDialog yesDialog = new ContentDialog()
                    {
                        Content = "用户取消本次配置。",
                        PrimaryButtonText = "关闭",
                    };
                }
            }

            if (!Application.Current.Windows.OfType<VoiceServicesEditor>().Any())
            {
                if (VsmSettings.AppSettings.AutoLoadEditor == true && !(ServerTabControl.SelectedIndex == -1))
                {
                    VoiceServicesEditor vSettingsEditor = new(VsmSettings.Servers, true, ServerTabControl.SelectedIndex);
                    vSettingsEditor.Show();
                }
                else
                {
                    VoiceServicesEditor vSettingsEditor = new(VsmSettings.Servers);
                    vSettingsEditor.Show();
                }
            }
        }

        private async void RemoveServerButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;

            if (server == null)
            {
                LogToConsole("错误：找不到要删除的选定服务器");
                return;
            }
            bool success = await RemoveServer(server);
            if (!success)
                LogToConsole("删除服务器时出错，或操作已中止。");
            else
                MainSettings.Save(VsmSettings);
        }

        private void ServerSettingsEditorButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Application.Current.Windows.OfType<ServerSettingsEditor>().Any())
            {
                if (VsmSettings.AppSettings.AutoLoadEditor == true && !(ServerTabControl.SelectedIndex == -1))
                {
                    ServerSettingsEditor sSettingsEditor = new(VsmSettings.Servers, true, ServerTabControl.SelectedIndex);
                    sSettingsEditor.Show();
                }
                else
                {
                    ServerSettingsEditor sSettingsEditor = new(VsmSettings.Servers);
                    sSettingsEditor.Show();
                }
            }
        }

        private async void ManageAdminsButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;

            if (!File.Exists(server.Path + @"\SaveData\Settings\adminlist.txt"))
            {
                ContentDialog closeFileDialog = new()
                {
                    Content = "找不到管理员文件(adminlist.txt)，请确保服务器安装正确。\n或尝试启动一次服务器",
                    PrimaryButtonText = "是",
                };
                await closeFileDialog.ShowAsync();
                //LogToConsole("找不到管理员文件(adminlist.txt)，请确保服务器安装正确。");
                return;
            }

            if (!Application.Current.Windows.OfType<AdminManager>().Any())
            {
                AdminManager aManager = new AdminManager(server);
                aManager.Show();
            }
        }

        private async void ServerFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;

            if (Directory.Exists(server.Path))
                Process.Start("explorer.exe", server.Path);
            else
            {
                ContentDialog closeFileDialog = new()
                {
                    Content = "找不到服务器文件夹。",
                    PrimaryButtonText = "是",
                };
                await closeFileDialog.ShowAsync();
            }
            //LogToConsole("找不到服务器文件夹。");
        }

        private void AddServerButton_Click(object sender, RoutedEventArgs e)
        {

            if (!Application.Current.Windows.OfType<CreateServer>().Any())
            {
                CreateServer cServer = new(VsmSettings);
                cServer.Show();
            }
        }

        private void ManageModsButton_Click(object sender, RoutedEventArgs e)
        {
            if (VsmSettings.Servers.Count == 0)
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "MOD管理器",
                    Content = $"没有添加任何服务器。请在尝试管理MODS之前至少添加一个服务器。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return;
            }

            if (!Application.Current.Windows.OfType<ModManager>().Any())
            {
                ModManager modManager = new(VsmSettings);
                modManager.Show();
            }
        }

        private void GameSettingsEditor_Click(object sender, RoutedEventArgs e)
        {
            if (!Application.Current.Windows.OfType<GameSettingsEditor>().Any())
            {
                if (VsmSettings.AppSettings.AutoLoadEditor == true && !(ServerTabControl.SelectedIndex == -1))
                {
                    GameSettingsEditor gSettingsEditor = new(VsmSettings.Servers, true, ServerTabControl.SelectedIndex);
                    gSettingsEditor.Show();
                }
                else
                {
                    GameSettingsEditor gSettingsEditor = new(VsmSettings.Servers);
                    gSettingsEditor.Show();
                }
            }
        }

        private void ManagerSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Application.Current.Windows.OfType<ManagerSettings>().Any())
            {
                ManagerSettings mSettings = new(VsmSettings);
                mSettings.Show();
            }
        }

        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            LookForUpdate();
        }

        private void RconServerButton_Click(object sender, RoutedEventArgs e)
        {
            Server server = ((Button)sender).DataContext as Server;

            if (!Application.Current.Windows.OfType<RconConsole>().Any())
            {
                RconConsole rConsole = new(server);
                rConsole.Show();
            }
        }

        private void TaskIcon_Click(object sender, RoutedEventArgs e)
        {

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }       

        private async void FixTools_Click(object sender, RoutedEventArgs e)
        {
            string workingDir = Directory.GetCurrentDirectory();
            string Thumbprint = "8da7f965ec5efc37910f1c6e59fdc1cc6a6ede16"; //CA证书指纹
            string registryPath = @"SOFTWARE\Microsoft\VisualStudio";
            int directxMajorVersion = 0;
            var OSVersion = Environment.OSVersion;

            // 检查证书
            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.MaxAllowed);
            X509Certificate2Collection collection = store.Certificates;
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            if (fcollection != null && fcollection.Count == 0)
            {
                ContentDialog yesNoDialog = new()
                {
                    Content = "没有 AmazonRootCA1 证书，是否导入？",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };
                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    string caCertPath = "./AmazonRootCA1.cer";

                    X509Certificate2 caCert = new X509Certificate2(caCertPath);
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(caCert);
                    store.Close();

                    LogToConsole("证书安装成功。");
                }
                else
                {
                    LogToConsole("用户取消了安装证书，退出此次修复。\r");
                    return;
                }
            }
            else
                LogToConsole("AmazonRootCA1 证书已存在于你的电脑中。\r");

            //检查VC++ runtime
            LogToConsole("检测是否已安装VC++ runtime。");
            RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath, true);

            if (key == null)
            {
                if (!File.Exists(workingDir + @"\vc_redist.x64.exe"))
                {
                    LogToConsole("VC++ runtime 不存在，正在下载发行包...");
                    byte[] fileBytes = await HttpClient.GetByteArrayAsync(@"https://aka.ms/vs/17/release/vc_redist.x64.exe");
                    await File.WriteAllBytesAsync(workingDir + @"\vc_redist.x64.exe", fileBytes);
                }

                if (File.Exists(workingDir + @"\vc_redist.x64.exe"))
                {
                    LogToConsole("正在安装VC++ runtime。");
                    Process process = new Process();
                    process.StartInfo.FileName = "vc_redist.x64.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.Arguments = "/install /quiet";
                    process.Start();
                    await Task.Delay(3000);
                }
            }
            else
            {
                LogToConsole("已存在VC++ runtime，跳过本次安装。\r");
            }
            LogToConsole("安装完成。\r");

            //检查DirectX
            if (OSVersion.Version.Major >= 6)
            {
                if (OSVersion.Version.Major > 6 || OSVersion.Version.Minor >= 1)
                {
                    directxMajorVersion = 11;
                }
                else
                {
                    directxMajorVersion = 10;
                }
            }
            if (11 == directxMajorVersion)
                LogToConsole("本机Directx版本信息正常，可尝试进入游戏查看。\r");
            else
            {
                LogToConsole("本机 Directx 版本信息错误，即将下载运行时，请稍等。");
                if (!File.Exists(workingDir + @"directx_Jun2010_redist.exe"))
                {
                    byte[] fileBytes = await HttpClient.GetByteArrayAsync(@"https://download.microsoft.com/download/8/4/a/84a35bf1-dafe-4ae8-82af-ad2ae20b6b14/directx_Jun2010_redist.exe");
                    await File.WriteAllBytesAsync(workingDir + @"\directx_Jun2010_redist.exe", fileBytes);
                    if (!Directory.Exists(workingDir + @"\directx_Jun2010_redist"))
                    {
                        LogToConsole("文件解压中");
                        Directory.CreateDirectory(workingDir + @"\directx_Jun2010_redist");
                        Process process = new Process();
                        process.StartInfo.FileName = "directx_Jun2010_redist.exe";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.Arguments = "/q /T:" + workingDir + "directx_Jun2010_redist' -Wait";
                        process.Start();
                        await Task.Delay(5000);
                        LogToConsole("解压完毕");
                        File.Delete(workingDir + @"\directx_Jun2010_redist.exe");
                    }

                    if (File.Exists(workingDir + @"\directx_Jun2010_redist\DXSETUP.exe") == true)
                    {
                        Process process = new Process();
                        process.StartInfo.FileName = "DXSETUP.exe";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.Arguments = "/silent";
                        process.Start();
                        LogToConsole("正在安装 DirectX, 大概需要30秒时间...");
                    }
                }
            }
            #endregion
        }
    }
}