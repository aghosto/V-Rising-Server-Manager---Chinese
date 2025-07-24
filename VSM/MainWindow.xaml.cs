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
using System.Text;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using static VRisingServerManager.LogManager;
using static VRisingServerManager.PlayerDataManager;
using LiveCharts;
using LiveCharts.Wpf;
using System.Net;

namespace VRisingServerManager;
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
    private PeriodicTimer? AutoRestartTimer;
    private RemoteConClient RCONClient;
    private ServerSpecSettings ServerSpecSettings = new();
    //private ChangeSaveFileEditor changeSaveFileEditor = new();


    LogManager LogManager = new();

    // 日志文件相对路径（基于当前服务器路径）
    private readonly Dictionary<LogType, string> _logTypeToTag = new Dictionary<LogType, string>
    {
        { LogType.VRising, @"logs\VRisingServer.log" },
        { LogType.BepinExOutput, @"BepInEx\LogOutput.log" },
        { LogType.BepinExError, @"BepInEx\ErrorLog.log" }
    };

    private Dictionary<string, LogType> _logTagToType;
    private Dictionary<LogType, RichTextBox> _logTypeToTexbox;
    private Dictionary<LogType, CheckBox> _logTypeToCheckbox;
    private string _activeLogType;

    // 日志监控器
    private Dictionary<LogType, FileSystemWatcher> _logWatchers = new Dictionary<LogType, FileSystemWatcher>();

    // 最大加载行数
    private const int MAX_LOG_LINES = 600;

    // 当前选中的服务器
    private Server _currentServer;

    // 定时器（用于定时检查日志更新）
    private DispatcherTimer _logUpdateTimer;
    public System.Timers.Timer playerUpdateTimer;

    // 记录上次检查时的文件大小
    private Dictionary<LogType, long> _lastFileSizes = new Dictionary<LogType, long>();

    // 存储已连接玩家的信息
    public Dictionary<ulong, VRisingPlayerInfo> _connectedPlayers = new Dictionary<ulong, VRisingPlayerInfo>();

    // 存储正在当前登录的玩家SteamID和时间
    private Dictionary<ulong, DateTime> _pendingConnections = new Dictionary<ulong, DateTime>();

    // 用于关联暂时分配的netEndpoint和玩家信息
    private Dictionary<string, VRisingPlayerInfo> _netEndpointToPlayer = new Dictionary<string, VRisingPlayerInfo>();
    private PlayerDataManager _playerDataManager;

    // 管理员列表文件路径
    private string AdminListPath => Path.Combine(_currentServer.Path, @"SaveData\Settings\adminlist.txt");

    public MainWindow()
    {
        if (!File.Exists(Directory.GetCurrentDirectory() + @"\VSMSettings.json"))
            MainSettings.Save(VsmSettings);
        else
            VsmSettings = MainSettings.LoadManagerSettings();

        DataContext = VsmSettings;

        if (VsmSettings.AppSettings.DarkMode == true)
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
        else
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;

        InitializeComponent();

        Closing += MainWindow_Closing;
            
        // 初始化日志控件映射
        _logTypeToTexbox = new Dictionary<LogType, RichTextBox>
        {
            { LogType.VRising, VRisingLogTextBox },
            { LogType.MainConsole, MainMenuConsoleTextBox },
            { LogType.BepinExOutput, BepinExOutputLogTextBox },
            { LogType.BepinExError, BepinExErrorLogTextBox }
        };

        _logTagToType = new Dictionary<string, LogType>
        {
            { "VRising", LogType.VRising },
            { "MainConsole", LogType.MainConsole },
            { "BepinExOutput", LogType.BepinExOutput },
            { "BepinExError", LogType.BepinExError },
        };

        // 初始化自动滚动控件映射
        _logTypeToCheckbox = new Dictionary<LogType, CheckBox>
        {
            { LogType.VRising, AutoScrollVRisingLog },
            { LogType.MainConsole, AutoScrollMainConsole },
            { LogType.BepinExOutput, AutoScrollBepinExOutputLog },
            { LogType.BepinExError, AutoScrollBepinExErrorLog }
        };

        // 初始化定时器（每1秒检查一次）
        _logUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _logUpdateTimer.Tick += LogUpdateTimer_Tick;
        _logUpdateTimer.Start();

        // 初始化日志定时器时调整间隔
        _logUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000)
        };

        // 绑定自动滚动复选框事件
        AutoScrollVRisingLog.Checked += AutoScrollCheckBox_CheckedChanged;
        AutoScrollVRisingLog.Unchecked += AutoScrollCheckBox_CheckedChanged;
        AutoScrollBepinExOutputLog.Checked += AutoScrollCheckBox_CheckedChanged;
        AutoScrollBepinExOutputLog.Unchecked += AutoScrollCheckBox_CheckedChanged;
        AutoScrollBepinExErrorLog.Checked += AutoScrollCheckBox_CheckedChanged;
        AutoScrollBepinExErrorLog.Unchecked += AutoScrollCheckBox_CheckedChanged;

        // 监听服务器选择变化
        ServerTabControl.SelectionChanged += async (s, e) =>
        {
            if (ServerTabControl.SelectedItem is Server selectedServer)
            {
                _currentServer = selectedServer;
                InitializeLogWatchers();
                InitializeServerStateListener();
                ReadLog(_currentServer);
                InitializePlayerDataManager(_currentServer);
                //InitServerStatusPanel();
                //UpdateServerStatusUI();
                UpdatePlayerCountText();
                if (!string.IsNullOrEmpty(_activeLogType))
                {
                    LoadLogByType(_logTagToType[_activeLogType], true);
                }
            }
        };


        VsmSettings.AppSettings.PropertyChanged += AppSettings_PropertyChanged;
        VsmSettings.Servers.CollectionChanged += Servers_CollectionChanged; // MVVM method not working
        VsmSettings.AppSettings.Version = new AppSettings().Version;

        // 初始化日志
        ShowLogMsg(LogType.MainConsole, $"夜族崛起服务端管理器(VSM)启动成功。", Brushes.Lime);
        ShowLogMsg(LogType.MainConsole,
            ((VsmSettings.Servers.Count > 0) ?
            $"{VsmSettings.Servers.Count} 个服务器从设置中加载成功。" :
            $"未找到服务器，请点击“添加服务器”以开始使用。"),
            VsmSettings.Servers.Count > 0 ? Brushes.Lime : Brushes.Yellow);

        ScanForServers();
        SetupTimer();
        SetAutoRestartTimer();

        if (File.Exists("VSMUpdater.exe") && File.Exists("VSMUpdater.deps.json") && File.Exists("VSMUpdater.dll") && File.Exists("VSMUpdater.runtimeconfig.json"))
        {
            File.Delete("VSMUpdater.exe");
            File.Delete("VSMUpdater.dll");
            File.Delete("VSMUpdater.deps.json");
            File.Delete("VSMUpdater.runtimeconfig.json");
            ShowLogMsg(LogType.MainConsole, $"旧版更新程序清理完成。", Brushes.Gray);
        }

        if (VsmSettings.AppSettings.AutoUpdateApp == true)
            LookForUpdate();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        MainSettings mainSettings = MainSettings.LoadManagerSettings();

        switch (mainSettings.AppSettings.CloseExecuteSelect)
        {
            case 0: 
                TrayIcon.Visibility = Visibility.Collapsed;
                TrayIcon.Dispose(); 
                break;

            case 1:
                e.Cancel = true;
                MinimizeToTray();
                break;
        }
    }

    // 最小化到托盘
    private void MinimizeToTray()
    {
        Hide();
        TrayIcon.Visibility = Visibility.Visible;
        TrayIcon.ShowBalloonTip("已最小化", "程序在托盘运行中", BalloonIcon.Info);
    }

    // 托盘菜单：显示窗口
    private void TrayIcon_ShowWindow(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        TrayIcon.Visibility = Visibility.Visible;
        Activate();
    }

    // 托盘菜单：退出程序
    private void TrayIcon_Exit(object sender, RoutedEventArgs e)
    {
        TrayIcon.Visibility = Visibility.Collapsed;
        TrayIcon.Dispose();

        Closing -= MainWindow_Closing;
        Close(); 
    }

    // 窗口关闭后释放资源
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (TrayIcon != null)
        {
            TrayIcon.Visibility = Visibility.Collapsed;
            TrayIcon.Dispose();
            TrayIcon = null;
        }

        Application.Current.Shutdown();
    }

    // 点击托盘显示或隐藏
    private void TrayIcon_Click(object sender, RoutedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
        }
    }

    // 自动滚动复选框状态变化处理
    private void AutoScrollCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && !string.IsNullOrEmpty(_activeLogType))
        {
            // 找到与当前复选框关联的日志类型
            LogType logType = new();
            foreach (var pair in _logTypeToCheckbox)
            {
                if (pair.Value == checkBox)
                {
                    logType = pair.Key;
                    break;
                }
            }

            if (logType == _logTagToType[_activeLogType] && checkBox.IsChecked == true)
            {
                _logTypeToTexbox[logType].ScrollToEnd();
            }
        }
    }

    private void LogUpdateTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            // 获取当前日志类型对应的LogType
            if (!_logTagToType.TryGetValue(_activeLogType, out LogType logType))
                return;
                
            if (logType == LogType.MainConsole)
                return;

            if (!_logTypeToTag.TryGetValue(logType, out string relativePath))
                return;

            if (_currentServer == null || _currentServer.Runtime?.State != ServerRuntime.ServerState.运行中)
                return;

            relativePath = Path.Combine(_currentServer.Path, relativePath);

            // 检查文件是否存在
            if (File.Exists(relativePath))
            {
                long currentSize = new FileInfo(relativePath).Length;
                bool sizeChanged = !_lastFileSizes.TryGetValue(logType, out long lastSize) || currentSize != lastSize;
                bool forceUpdate = DateTime.Now.Second % 10 == 0;

                if (sizeChanged || forceUpdate)
                {
                    _lastFileSizes[logType] = currentSize;
                    OnLogFileChanged(logType);
                }
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"定时器检查日志更新失败：{ex.Message}", Brushes.Red);
        }
    }

    // 初始化日志监控器
    private void InitializeLogWatchers()
    {
        // 清除现有监控器
        foreach (var watcher in _logWatchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _logWatchers.Clear();

        if (_currentServer == null || !Directory.Exists(_currentServer.Path))
            return;

        foreach (var logType in _logTypeToTag.Keys)
        {
            string fullPath = Path.Combine(_currentServer.Path, _logTypeToTag[logType]);
            string dir = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);

            if (Directory.Exists(dir))
            {
                var watcher = new FileSystemWatcher(dir, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = false
                };
                watcher.Changed += (s, e) => OnLogFileChanged(logType);
                watcher.Renamed += (s, e) => OnLogFileChanged(logType);
                _logWatchers[logType] = watcher;
            }
        }
    }

    // 日志标签页切换事件（强制更新）
    private void LogTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LogTabControl.SelectedItem is TabItem selectedTab && selectedTab.Tag is string logType)
        {
            // 先暂停其他的所有标签页
            foreach (var watcher in _logWatchers.Values)
            {
                watcher.EnableRaisingEvents = false;
            }
            _activeLogType = logType;
            LoadLogByType(_logTagToType[logType], forceRefresh: true);
            if (_currentServer.Runtime.State == ServerRuntime.ServerState.运行中)
            {
                StartActiveLogWatcher();
            }
        }
    }

    // 根据窗口类型加载日志文件
    private void LoadLogByType(LogType logType, bool forceRefresh = false)
    {
        if (logType == LogType.MainConsole)
            return;

        if (_currentServer == null || !_logTypeToTexbox.ContainsKey(logType))
            return;
            
        RichTextBox logBox = _logTypeToTexbox[logType];
        string fullPath = Path.Combine(_currentServer.Path, _logTypeToTag[logType]);

        try
        {
            // 仅在强制刷新或文件大小变化时重新加载
            if (forceRefresh || !_lastFileSizes.TryGetValue(logType, out long lastSize) ||
                new FileInfo(fullPath).Length != lastSize)
            {
                logBox.Document.Blocks.Clear();

                if (!File.Exists(fullPath))
                {
                    ShowLogMsg(logType, $"日志文件不存在：{fullPath}", Brushes.Yellow);
                    ShowLogMsg(logType, $"请确保服务器有正常启动过至少一次", Brushes.Yellow);
                    if (logBox != VRisingLogTextBox)
                    {
                        ShowLogMsg(logType, $"或当前服务器并不是Mod服务器", Brushes.Yellow);
                    }
                    return;
                }

                string[] lines = ReadLastNLines(fullPath, MAX_LOG_LINES);
                foreach (string line in lines)
                {
                    AppendLogLine(logType, line);
                }

                _lastFileSizes[logType] = new FileInfo(fullPath).Length;

                ShowLogMsg(logType, $"已加载最近 {lines.Length} 行日志", Brushes.Gray);
            }

            if (_logTypeToCheckbox[logType].IsChecked == true)
            {
                logBox.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(logType, $"加载失败：{ex.Message}", Brushes.Red);
        }
    }

    // 读取文件的最后N行
    private string[] ReadLastNLines(string filePath, int lineCount)
    {
        List<string> lines = new List<string>();

        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // 强制刷新流，确保读取最新内容
                stream.Position = 0;
                reader.DiscardBufferedData();

                string[] buffer = new string[lineCount];
                int bufferIndex = 0;
                int totalLines = 0;

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line != null) // 避免空行干扰
                    {
                        buffer[bufferIndex] = line;
                        bufferIndex = (bufferIndex + 1) % lineCount;
                        totalLines++;
                    }
                }

                // 提取有效行（处理文件截断场景）
                int startIndex = totalLines > lineCount ? bufferIndex : 0;
                int count = Math.Min(lineCount, totalLines);

                for (int i = 0; i < count; i++)
                {
                    string line = buffer[(startIndex + i) % lineCount];
                    if (!string.IsNullOrEmpty(line)) // 过滤空行
                    {
                        lines.Add(line);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lines.Clear();
            lines.Add($"[警告] 读取日志失败：{ex.Message}");
        }

        return lines.ToArray();
    }

    private void OnLogFileChanged(LogType logType)
    {
        if (_currentServer?.Runtime?.State != ServerRuntime.ServerState.运行中)
            return;

        string logPath = Path.Combine(_currentServer.Path, _logTypeToTag[logType]);
        if (!File.Exists(logPath)) return;

        try
        {
            var logTag = _logTagToType.FirstOrDefault(t => t.Value == logType).Key;
            if (!string.IsNullOrEmpty(logTag) && _activeLogType == logTag)
            {
                Dispatcher.Invoke(() => LoadLogByType(logType, forceRefresh: true));
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"更新 {logType} 日志失败: {ex.Message}", Brushes.Red);
        }
    }

    // 更新服务器状态面板数据
    private void UpdateServerStatusUI()
    {
        if (_currentServer == null) return;

        if (ServerNameText != null)
            ServerNameText.Text = _currentServer.vsmServerName;

        var serverState = _currentServer.Runtime?.State ?? ServerRuntime.ServerState.已停止;
        string statusText = serverState == ServerRuntime.ServerState.运行中 ? "运行中" : "已停止";
        Brush statusBrush = serverState == ServerRuntime.ServerState.运行中 ? Brushes.Green : Brushes.Red;

        if (ServerStatusText != null)
        {
            ServerStatusText.Text = statusText;
            ServerStatusText.Foreground = statusBrush;
        }
        
        if (LastUpdatedText != null)
            LastUpdatedText.Text = $"最后更新: {DateTime.Now:HH:mm:ss}";
    }


    // 服务器状态变化时的处理
    private void OnServerStateChanged()
    {
        if (_currentServer == null || _currentServer.Runtime == null) 
            return;

        ServerRuntime.ServerState currentState = _currentServer.Runtime.State;

        Dispatcher.Invoke(() =>
        {
            if (currentState == ServerRuntime.ServerState.运行中)
            {
                UpdateServerStatusUI();
                if (!string.IsNullOrEmpty(_activeLogType) && _logWatchers.TryGetValue(_logTagToType[_activeLogType], out var watcher))
                {
                    watcher.EnableRaisingEvents = true;
                    //ShowLogMsg(_logTagToType[_activeLogType], "服务器正在运行，日志将实时更新", Brushes.Lime);
                }
            }
            else
            {
                UpdateServerStatusUI();
                foreach (var watcher in _logWatchers.Values)
                {
                    watcher.EnableRaisingEvents = false;
                }

                if (!string.IsNullOrEmpty(_activeLogType))
                {
                    //ShowLogMsg(_logTagToType[_activeLogType], $"服务器状态：{currentState}，日志已停止更新", Brushes.Gray);
                }
            }
        });
    }

    // 初始化服务器状态监听
    private void InitializeServerStateListener()
    {
        if (_currentServer != null && _currentServer.Runtime != null)
        {
            _currentServer.Runtime.PropertyChanged -= OnRuntimePropertyChanged;
            _currentServer.Runtime.PropertyChanged += OnRuntimePropertyChanged;

            OnServerStateChanged();
        }
    }

    // 服务器运行时属性变化处理
    private void OnRuntimePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerRuntime.State))
        {
            OnServerStateChanged();
        }
    }

    // 刷新日志按钮点击（强制更新）
    private void RefreshLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string logType)
        {
            LoadLogByType(_logTagToType[logType], true);
        }
    }

    // 清空日志按钮点击
    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string logType && _logTagToType.ContainsKey(logType))
        {
            _logTypeToTexbox[_logTagToType[logType]].Document.Blocks.Clear();
            ShowLogMsg(_logTagToType[logType], "日志已清空", Brushes.Gray);
        }
    }

    // 保存日志按钮点击
    private async void SaveLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string logType && _logTagToType.ContainsKey(logType))
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = Path.GetFileName(_logTypeToTag[_logTagToType[logType]])
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                    {
                        var textRange = new TextRange(
                            _logTypeToTexbox[_logTagToType[logType]].Document.ContentStart,
                            _logTypeToTexbox[_logTagToType[logType]].Document.ContentEnd);
                        writer.Write(textRange.Text);
                    }
                    await ShowErrorDialog($"日志已保存至：{saveDialog.FileName}");
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog($"保存失败：{ex.Message}");
                }
            }
        }
    }

    // 打开日志按钮点击事件
    private async void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. 获取当前服务器实例
        //Server server = ((Button)sender).DataContext as Server;
        if (_currentServer == null)
        {
            await ShowErrorDialog($"未找到对应的服务器实例");
            return;
        }

        // 2. 验证按钮标签和日志类型映射
        if (sender is not Button btn || btn.Tag is not string logType || !_logTagToType.ContainsKey(logType))
        {
            ShowLogMsg(LogType.MainConsole, $"日志类型配置错误", Brushes.Red);
            return;
        }

        try
        {
            string logPath = string.Empty;
            switch (_logTagToType[logType])
            {
                case LogType.VRising:
                    logPath = Path.Combine(_currentServer.Path, "logs", "VRisingServer.log");
                    break;
                case LogType.BepinExOutput:
                    logPath = Path.Combine(_currentServer.Path, "BepInEx", "LogOutput.log");
                    break;
                case LogType.BepinExError:
                    logPath = Path.Combine(_currentServer.Path, "BepInEx", "ErrorLog.log");
                    break;
                default:
                    await ShowErrorDialog($"不支持的日志类型");
                    return;
            }

            if (string.IsNullOrEmpty(logPath))
            {
                await ShowErrorDialog($"日志文件不存在：{logPath}");
                return;
            }

            if (!File.Exists(logPath))
            {
                await ShowErrorDialog($"日志文件不存在：{logPath}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,      
                UseShellExecute = true,  
                Verb = "open"            
            });

            //ShowLogMsg(LogType.MainConsole, $"已用默认程序打开日志：{logPath}", Brushes.Green);
        }
        catch (Exception ex)
        {
            // 捕获所有异常（如权限不足、无默认程序关联等）
            await ShowErrorDialog($"打开日志失败：{ex.Message}");
        }
    }

    public static async Task ShowErrorDialog(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "操作失败",
            Content = message,
            CloseButtonText = "确定"
        };
        await dialog.ShowAsync();
    }

    private void AppendLogLine(LogType logType, string line)
    {
        var paragraph = new Paragraph();
        paragraph.Foreground = GetLogColor(line);
        paragraph.Inlines.Add(new Run(line));
        _logTypeToTexbox[logType].Document.Blocks.Add(paragraph);
    }

    public void InternalShowLogMsg(LogType logType, string message, Brush color)
    {
        RichTextBox targetTextBox = _logTypeToTexbox[logType];
        if (targetTextBox != null)
        {
            string timestampedMessage = $"[{GetTimestamp("log")}]  {message}";
            Paragraph paragraph = new Paragraph(new Run(timestampedMessage));
            paragraph.Foreground = color;

            // 消除段落间距
            paragraph.Margin = new Thickness(0);
            targetTextBox.Document.Blocks.Add(paragraph);
            if (_logTypeToCheckbox[logType]?.IsChecked == true)
            {
                targetTextBox.ScrollToEnd();
            }
        }

    }
    public void ShowLogMsg(LogType logType, string message, Brush color)
    {
        if (Dispatcher.CheckAccess())
        {
            InternalShowLogMsg(logType, message, color);
        }
        else
        {
            Dispatcher.Invoke(() => InternalShowLogMsg(logType, message, color));
        }
    }

    private Brush GetLogColor(string line)
    {
        if (string.IsNullOrEmpty(line)) return Brushes.White;
        string lowerLine = line.ToLower();

        if (lowerLine.Contains("[error]") || lowerLine.Contains("exception"))
            return Brushes.Red;
        if (lowerLine.Contains("[warning]") || lowerLine.Contains("warn") || lowerLine.Contains("internal") || lowerLine.Contains("to debug"))
            return Brushes.Yellow;
        if (lowerLine.Contains("[info]"))
            return Brushes.LimeGreen;
        return Brushes.White;
    }

    private void StartActiveLogWatcher()
    {
        if (_logWatchers.TryGetValue(_logTagToType[_activeLogType], out var watcher))
        {
            watcher.EnableRaisingEvents = true;
        }
    }

    private async void LookForUpdate()
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;
        int attempt = 0;

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "VSM-Client/1.0");

        while (attempt < maxRetries)
        {
            attempt++;
            try
            {
                ShowLogMsg(LogType.MainConsole, $"检查更新中 ({attempt}/{maxRetries})...", Brushes.Gray);

                HttpResponseMessage response = await httpClient.GetAsync("https://gitee.com/aGHOSToZero/V-Rising-Server-Manager---Chinese/raw/master/VERSION");
                response.EnsureSuccessStatusCode();

                string latestVersion = await response.Content.ReadAsStringAsync();
                latestVersion = latestVersion.Trim();

                if (latestVersion != VsmSettings.AppSettings.Version)
                {
                    VsmSettings.AppSettings.HasNewVersion = true;
                    VsmSettings.AppSettings.NewVersion = latestVersion;
                    ShowLogMsg(LogType.MainConsole, $"发现新版本：{latestVersion}，可点击左下角版本按钮更新软件", Brushes.Yellow);
                }
                else
                {
                    VsmSettings.AppSettings.HasNewVersion = false;
                    ShowLogMsg(LogType.MainConsole, $"正在运行最新的版本：{latestVersion}", Brushes.Lime);
                }

                return; 
            }
            catch (HttpRequestException ex)
            {
                string errorMessage = "未知网络错误";

                if (ex.InnerException != null)
                {
                    if (ex.InnerException.Message.Contains("EOF") || ex.InnerException.Message.Contains("0 bytes"))
                    {
                        errorMessage = "服务器提前关闭了连接";
                    }
                    else if (ex.InnerException.Message.Contains("timed out"))
                    {
                        errorMessage = "连接超时，请检查网络";
                    }
                    else if (ex.InnerException.Message.Contains("host"))
                    {
                        errorMessage = "无法解析主机名，请检查网络连接";
                    }
                    else if (ex.InnerException is System.Security.Authentication.AuthenticationException)
                    {
                        errorMessage = "SSL 认证失败，可能是证书问题或 TLS 版本不兼容";
                    }
                }
                else if (ex.StatusCode.HasValue)
                {
                    errorMessage = $"服务器返回错误: {ex.StatusCode}";
                }

                if (attempt < maxRetries)
                {
                    ShowLogMsg(LogType.MainConsole, $"检查更新失败 ({attempt}/{maxRetries}): {errorMessage}，正在重试...", Brushes.Orange);
                    await Task.Delay(retryDelayMs);
                }
                else
                {
                    ShowLogMsg(LogType.MainConsole, $"搜索软件更新失败: {errorMessage}", Brushes.Red);
                    ShowLogMsg(LogType.MainConsole, $"请检查网络连接或稍后再试", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                ShowLogMsg(LogType.MainConsole, $"搜索软件更新时发生未知错误: {ex.Message}", Brushes.Red);
                break;
            }
        }
    }

    /// <summary>
    /// Sets up the timer for AutoUpdates
    /// </summary>
    public void SetupTimer()
    {
        if (VsmSettings.AppSettings.AutoUpdate == true)
        {
#if DEBUG
            AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
            AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
            AutoUpdateLoop();
        }
    }


    public void SetAutoRestartTimer()
    {
        if (VsmSettings.AppSettings.EnableAutoRestart == true)
        {
            ShowLogMsg(LogType.MainConsole, $"自动重启已启动，重启时间为每日的 {VsmSettings.AppSettings.AutoRestartHour} 时 {VsmSettings.AppSettings.AutoRestartMin} 分 {VsmSettings.AppSettings.AutoRestartSec} 秒。", Brushes.Yellow);
            AutoRestartTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            AutoRestartLoop();
        }
    }

    private async void AutoRestartLoop()
    {
        while (await AutoRestartTimer.WaitForNextTickAsync())
        {
            if (VsmSettings.AppSettings.ManagerSettingsClose)
            {
                if (VsmSettings.AppSettings.EnableAutoRestart)
                {
                    ShowLogMsg(LogType.MainConsole, $"重载自动重启时间，重启时间为每日的 {VsmSettings.AppSettings.AutoRestartHour} 时 {VsmSettings.AppSettings.AutoRestartMin} 分 {VsmSettings.AppSettings.AutoRestartSec} 秒。", Brushes.Yellow);
                    VsmSettings.AppSettings.ManagerSettingsClose = false;
                }
                bool timetoRestart = await CheckForRestart();
                if (timetoRestart == true && VsmSettings.Servers.Count > 0)
                    AutoRestart();
            }
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

    private async Task<bool> CheckForRestart()
    {
        bool timetoRestart = false;
        if (DateTime.Now.Hour == VsmSettings.AppSettings.AutoRestartHour && 
                DateTime.Now.Minute == VsmSettings.AppSettings.AutoRestartMin && 
                    DateTime.Now.Second == VsmSettings.AppSettings.AutoRestartSec)
            AutoRestart();
        return timetoRestart;
    }

    private async void AutoRestart()
    {
        List<Task> serverTasks = new List<Task>();
        List<Server> runningServers = new List<Server>();

        foreach (Server server in VsmSettings.Servers)
        {
            if (server.Runtime.State == ServerRuntime.ServerState.运行中)
            {
                server.Runtime.UserStopped = true;

                runningServers.Add(server);
            }
        }

        if (runningServers.Count > 0)
        {
            //SendDiscordMessage(VsmSettings.WebhookSettings.UpdateWait);
            await Task.Delay(TimeSpan.FromSeconds(0));
        }
        else
        {
            ShowLogMsg(LogType.MainConsole, $"当前无正在运行的服务器，自动重启未生效。\r", Brushes.Yellow);
            return;
        }

        ShowLogMsg(LogType.MainConsole, $"正在自动重启 {runningServers.Count} 个服务器。" + ((runningServers.Count > 0) ? $"，在此之前即将关闭 {runningServers.Count} 个服务器。" : ""), Brushes.Yellow);
        foreach (Server server in runningServers)
        {
            await RestartServer(server);
        }
        ShowLogMsg(LogType.MainConsole, $"自动重启完成。", Brushes.Lime);

    }

    private void SendDiscordMessage(string message)
    {
        if (VsmSettings.WebhookSettings.Enabled == false || message == "")
            return;

        if (VsmSettings.WebhookSettings.URL == "")
        {
            //ShowLogMsg(LogType.MainConsole, "Discord webhook尝试发送消息，但URL未定义。", Brushes.Yellow);
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
        ShowLogMsg(LogType.MainConsole, "未找到SteamCMD，正在下载...", Brushes.Yellow);
        byte[] fileBytes = await HttpClient.GetByteArrayAsync(@"https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
        await File.WriteAllBytesAsync(workingDir + @"\steamcmd.zip", fileBytes);
        if (File.Exists(workingDir + @"\SteamCMD\steamcmd.exe") == true)
        {
            File.Delete(workingDir + @"\SteamCMD\steamcmd.exe");
        }
        ShowLogMsg(LogType.MainConsole, "解压中...", Brushes.Yellow);
        ZipFile.ExtractToDirectory(workingDir + @"\steamcmd.zip", workingDir + @"\SteamCMD");
        if (File.Exists(workingDir + @"\steamcmd.zip"))
        {
            File.Delete(workingDir + @"\steamcmd.zip");
        }

        ShowLogMsg(LogType.MainConsole, "正在获取V Rising Dedicated Server应用信息。", Brushes.Lime);
        await CheckForUpdate();

        return true;
    }

    private async Task<bool> UpdateGame(Server server)
    {
        if (server.Runtime.State == ServerRuntime.ServerState.更新中)
        {
            ShowLogMsg(LogType.MainConsole, $"服务器 {server.vsmServerName} 正在更新中，尝试终止现有SteamCMD进程...", Brushes.Yellow);
            KillAllSteamcmdProcesses();
            server.Runtime.State = ServerRuntime.ServerState.已停止;
            return false;
        }
        if (server.Runtime.State != ServerRuntime.ServerState.已停止)
        {
            ShowLogMsg(LogType.MainConsole, $"服务器 {server.vsmServerName} 状态为 {server.Runtime.State}，无法更新（仅允许已停止状态）", Brushes.Red);
            return false;
        }
        server.Runtime.State = ServerRuntime.ServerState.更新中;
        Process steamcmd = null;

        Dispatcher.Invoke(() =>
        {
            InstallationProgressBar.IsIndeterminate = true;
            InstallationProgressBar.Visibility = Visibility.Visible;
        });

        if (!Directory.Exists(server.Path))
        {
            ShowLogMsg(LogType.MainConsole, $"服务器目录不存在，正在创建: {server.Path}", Brushes.Lime);
            Directory.CreateDirectory(server.Path);
        }

        if (server.Runtime.Process != null && !server.Runtime.Process.HasExited)
        {
            ShowLogMsg(LogType.MainConsole, $"服务器 {server.vsmServerName} 仍在运行中，无法更新", Brushes.Yellow);
            server.Runtime.State = ServerRuntime.ServerState.已停止; // 重置状态
            return false;
        }

        string steamCmdDir = Path.Combine(Directory.GetCurrentDirectory(), @"SteamCMD");
        string steamCmdPath = Path.Combine(steamCmdDir, "steamcmd.exe");

        if (!File.Exists(steamCmdPath))
        {
            ShowLogMsg(LogType.MainConsole, "未找到SteamCMD，正在下载...", Brushes.Lime);

            try
            {
                using var httpClient = new HttpClient();
                byte[] fileBytes = await httpClient.GetByteArrayAsync(@"https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip");
                string zipPath = Path.Combine(Directory.GetCurrentDirectory(), "steamcmd.zip");
                await File.WriteAllBytesAsync(zipPath, fileBytes);

                if (!Directory.Exists(steamCmdDir))
                    Directory.CreateDirectory(steamCmdDir);

                ZipFile.ExtractToDirectory(zipPath, steamCmdDir);
                File.Delete(zipPath);
                ShowLogMsg(LogType.MainConsole, "SteamCMD下载并安装成功", Brushes.Lime);
            }
            catch (Exception ex)
            {
                ShowLogMsg(LogType.MainConsole, $"SteamCMD下载失败：{ex.Message}", Brushes.Red);
                server.Runtime.State = ServerRuntime.ServerState.已停止;
                return false;
            }
        }

        bool isNewInstall = !Directory.EnumerateFiles(server.Path).Any();
        string action = isNewInstall ? "下载" : "更新";

        ShowLogMsg(LogType.MainConsole, $"正在{action}游戏服务器：{server.vsmServerName}，请等待...", Brushes.Lime);
        //ShowLogMsg(LogType.MainConsole, $"若{action}成功但启动失败，请到设置中开启“显示SteamCMD窗口”", Brushes.Gray);

        if (VsmSettings.AppSettings == null)
        {
            ShowLogMsg(LogType.MainConsole, "警告：应用设置未初始化，使用默认值", Brushes.Yellow);
            VsmSettings.AppSettings = new AppSettings();
        }

        string[] installScript = {
            $"force_install_dir \"{server.Path}\"",
            "login anonymous",
            VsmSettings.AppSettings.VerifyUpdates ? "app_update 1829350 validate" : "app_update 1829350",
            "quit"
        };

        string scriptPath = Path.Combine(server.Path, "steamcmd.txt");
        if (File.Exists(scriptPath))
            File.Delete(scriptPath);
        File.WriteAllLines(scriptPath, installScript);

        string parameters = $@"+runscript ""{scriptPath}""";

        bool hasError = false;
        CancellationTokenSource cts = new CancellationTokenSource(); // 用于取消读取

        try
        {
            steamcmd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = steamCmdPath,
                    Arguments = parameters,
                    CreateNoWindow = true,
                    UseShellExecute = VsmSettings.AppSettings.ShowSteamWindow ? true : false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = server.Path,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            steamcmd.Start();
            steamcmd.OutputDataReceived += (sender, e) =>
            {
                if (hasError)
                    return;

                if (!string.IsNullOrEmpty(e.Data))
                {
                    // 检测到中文路径错误
                    if (e.Data.Contains("默认文件夹"))
                    {
                        ShowLogMsg(LogType.MainConsole, "错误：路径包含中文，请不要在带有中文的目录中使用！", Brushes.Red);
                        hasError = true;
                        KillAllSteamcmdProcesses(); 
                        return;
                    }

                    if (e.Data.Contains("FAILED (No Connection)"))
                    {
                        ShowLogMsg(LogType.MainConsole, "错误：服务器更新失败，请检查你的网络连接！", Brushes.Red);
                        hasError = true;
                        KillAllSteamcmdProcesses();
                        return;
                    }
                }
            };

            // 启动进程并异步读取输出
            steamcmd.BeginOutputReadLine();
            steamcmd.BeginErrorReadLine();
            await steamcmd.WaitForExitAsync();

            if (hasError)
            {
                ShowLogMsg(LogType.MainConsole, $"{action}被强制终止（检测到错误）", Brushes.Red);
                server.Runtime.State = ServerRuntime.ServerState.已停止;
                return false;
            }

            if (steamcmd.ExitCode == 0 && !hasError)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowLogMsg(LogType.MainConsole, $"{action}成功：{server.vsmServerName}", Brushes.Lime);
                    LogTextBox.Text = $"{action}完成！";
                });
                server.Runtime.State = ServerRuntime.ServerState.已停止;
                return true;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    ShowLogMsg(LogType.MainConsole, $"{action}失败，退出代码：{steamcmd.ExitCode}", Brushes.Red);
                    LogTextBox.Text = $"{action}失败";
                });
                server.Runtime.State = ServerRuntime.ServerState.已停止;
                return false;
            }
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ShowLogMsg(LogType.MainConsole, $"{action}出错：{ex.Message}", Brushes.Red);
                LogTextBox.Text = "操作出错";
            });
            server.Runtime.State = ServerRuntime.ServerState.已停止;
            return false;
        }
        finally
        {
            cts.Cancel();
            Dispatcher.Invoke(() =>
            {
                InstallationProgressBar.IsIndeterminate = false;
                InstallationProgressBar.Visibility = Visibility.Collapsed;
            });
            steamcmd?.Dispose();
            if (File.Exists(scriptPath))
            {
                try
                { 
                    File.Delete(scriptPath); 
                }
                catch 
                { 

                }
            }
        }
    }
    private void KillAllSteamcmdProcesses()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("steamcmd"))
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                    ShowLogMsg(LogType.MainConsole, $"终止SteamCMD进程（PID: {process.Id}）", Brushes.Yellow);
                }
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"终止SteamCMD进程失败：{ex.Message}", Brushes.Red);
        }
    }

    private async Task<bool> StartServer(Server server)
    {
        if (server.Runtime.Process != null)
        {
            ShowLogMsg(LogType.MainConsole, $"错误：{server.vsmServerName} 已在运行中", Brushes.Red);
            return false;
        }

        try
        {
            // 规范化路径
            string saveDataPath = Path.Combine(server.Path, "SaveData", "Settings");
            string defaultSettingsPath = Path.Combine(server.Path, "VRisingServer_Data", "StreamingAssets", "Settings");

            // 确保保存目录存在
            if (!Directory.Exists(saveDataPath))
            {
                Directory.CreateDirectory(saveDataPath);
                ShowLogMsg(LogType.MainConsole, $"创建目录: {saveDataPath}", Brushes.Yellow);
            }

            // 确保配置文件存在
            string hostSettingsPath = Path.Combine(saveDataPath, "ServerHostSettings.json");
            string gameSettingsPath = Path.Combine(saveDataPath, "ServerGameSettings.json");

            if (!File.Exists(hostSettingsPath) || !File.Exists(gameSettingsPath))
            {
                // 检查默认配置文件是否存在
                string defaultHostSettingsPath = Path.Combine(defaultSettingsPath, "ServerHostSettings.json");
                string defaultGameSettingsPath = Path.Combine(defaultSettingsPath, "ServerGameSettings.json");

                if (!File.Exists(defaultHostSettingsPath) || !File.Exists(defaultGameSettingsPath))
                {
                    ShowLogMsg(LogType.MainConsole, "错误：找不到默认配置文件", Brushes.Red);
                    return false;
                }

                // 复制默认配置文件
                File.Copy(defaultHostSettingsPath, hostSettingsPath, true);
                File.Copy(defaultGameSettingsPath, gameSettingsPath, true);
                ShowLogMsg(LogType.MainConsole, "已复制默认配置文件", Brushes.Lime);
            }

            // 读取服务器配置
            string jsonString = File.ReadAllText(hostSettingsPath);
            ServerSettings jsonObject = JsonConvert.DeserializeObject<ServerSettings>(jsonString);

            ShowLogMsg(LogType.MainConsole,
                $"启动服务器：{jsonObject.Name} | VSM内名称：{server.vsmServerName} | 显示名称：{server.LaunchSettings.DisplayName}",
                Brushes.Lime);

            // 等待服务器初始化
            await Task.Delay(1000);

            // 启动服务器进程
            string serverExePath = Path.Combine(server.Path, "VRisingServer.exe");
            if (!File.Exists(serverExePath))
            {
                ShowLogMsg(LogType.MainConsole, "错误：未找到VRisingServer.exe", Brushes.Red);
                return false;
            }

            ShowLogMsg(LogType.MainConsole,
                $"启动服务器：{server.vsmServerName}{(server.Runtime.RestartAttempts > 0 ? $" 尝试 {server.Runtime.RestartAttempts}/3" : "")}",
                Brushes.Lime);

            if (VsmSettings.WebhookSettings.Enabled && !string.IsNullOrEmpty(server.WebhookMessages.StartServer) && server.WebhookMessages.Enabled)
            {
                SendDiscordMessage(server.WebhookMessages.StartServer);
            }

            // 构建启动参数
            string parameters = $@"-persistentDataPath ""{Path.Combine(server.Path, "SaveData")}"" 
                              -serverName ""{jsonObject.Name}"" 
                              -saveName ""{server.LaunchSettings.WorldName}"" 日志处理错误
                              -logFile ""{Path.Combine(server.Path, "logs", "VRisingServer.log")}""
                              {(server.LaunchSettings.BindToIP ? $@" -address ""{server.LaunchSettings.BindingIP}""" : "")}";

            // 启动服务器进程
            Process serverProcess = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Normal,
                    FileName = serverExePath,
                    UseShellExecute = true,
                    Arguments = parameters
                },
                EnableRaisingEvents = true
            };

            ShowLogMsg(LogType.MainConsole, "正在载入配置文件...", Brushes.Lime);

            serverProcess.Exited += (sender, e) => ServerProcessExited(sender, e, server);
            serverProcess.Start();

            server.Runtime.State = ServerRuntime.ServerState.运行中;
            server.Runtime.UserStopped = false;
            server.Runtime.Process = serverProcess;

            ShowLogMsg(LogType.MainConsole,
                $"启动服务器完成：{jsonObject.Name} | VSM抬头名称：{server.vsmServerName} | 显示名称：{server.LaunchSettings.DisplayName}",
                Brushes.Lime);

            return true;
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"启动服务器失败：{ex.Message}", Brushes.Red);
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
                    server.Runtime.State = ServerRuntime.ServerState.运行中;
                    process.EnableRaisingEvents = true;
                    process.Exited += new EventHandler((sender, e) => ServerProcessExited(sender, e, server));
                    server.Runtime.Process = process;
                    foundServers++;
                }
            }
        }

        foreach (Server server in VsmSettings.Servers)
        {
            if (server.AutoStart == true && server.Runtime.State == ServerRuntime.ServerState.已停止)
            {
                StartServer(server);
            }
        }

        if (foundServers > 0)
        {
            ShowLogMsg(LogType.MainConsole, $"已找到 {foundServers} 个服务器正在运行。", Brushes.Lime);
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
            if (server.Runtime.State == ServerRuntime.ServerState.运行中)
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

        ShowLogMsg(LogType.MainConsole, $"正在自动更新 {VsmSettings.Servers.Count} 个服务器。" + ((runningServers.Count > 0) ? $"在此之前即将关闭 {runningServers.Count} 个服务器。" : ""), Brushes.Yellow);

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
        ShowLogMsg(LogType.MainConsole, $"自动更新完成。", Brushes.Lime);
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
        ShowLogMsg(LogType.MainConsole, $"正在查询服务器更新...", Brushes.Yellow);
        string json = await HttpClient.GetStringAsync("https://api.steamcmd.net/v1/info/1829350");
        JsonNode jsonNode = JsonNode.Parse(json);

        var version = jsonNode!["data"]["1829350"]["depots"]["branches"]["public"]["timeupdated"]!.ToString();

        if (version == VsmSettings.AppSettings.LastUpdateTimeUNIX)
        {
            VsmSettings.AppSettings.LastUpdateTimeUNIX = version;
            foundUpdate = false;
            if (VsmSettings.AppSettings.LastUpdateTimeUNIX != "")
                VsmSettings.AppSettings.LastUpdateTime = "服务器最近更新的时间：" + DateTimeOffset.FromUnixTimeSeconds(long.Parse(VsmSettings.AppSettings.LastUpdateTimeUNIX)).DateTime.ToString();

            MainSettings.Save(VsmSettings);
            ShowLogMsg(LogType.MainConsole, $"当前游戏服务器已是最新版本。", Brushes.Lime);
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
            VsmSettings.AppSettings.LastUpdateTime = "服务器上一次更新的时间：" + DateTimeOffset.FromUnixTimeSeconds(long.Parse(VsmSettings.AppSettings.LastUpdateTimeUNIX)).DateTime.ToString();

        MainSettings.Save(VsmSettings);
        return foundUpdate;
    }

    //private async void ReadLog(Server server)
    //{
    //    using (FileStream fs = new FileStream(server.Path + @"\logs\VRisingServer.log", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
    //    using (StreamReader sr = new StreamReader(fs))
    //    {
    //        string ipAddress = "";
    //        string steamID = "";
    //        int foundVariables = 0;

    //        while (foundVariables < 3 && server.Runtime.Process != null)
    //        {
    //            try
    //            {
    //                string line = await sr.ReadLineAsync();
    //                if (line != null)
    //                {
    //                    if (line.Contains("PlatformSystemBase - OnPolicyResponse - Public IP: "))
    //                    {
    //                        ipAddress = line.Split("PlatformSystemBase - OnPolicyResponse - Public IP: ")[1];
    //                        foundVariables++;
    //                    }
    //                    if (line.Contains("SteamNetworking - Successfully logged in with the SteamGameServer API. SteamID: "))
    //                    {
    //                        steamID = line.Split("SteamNetworking - Successfully logged in with the SteamGameServer API. SteamID: ")[1];
    //                        foundVariables++;
    //                    }
    //                    if (line.Contains("Shutting down Asynchronous Streaming"))
    //                        foundVariables++;
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                ShowLogMsg(LogType.MainConsole, $"错误：{ex.Message}", Brushes.Orange);
    //            }
    //        }

    //        ShowLogMsg(LogType.MainConsole, $"Public IP: {ipAddress}", Brushes.Orange);
    //        ShowLogMsg(LogType.MainConsole, $"Game Server SteamID: {steamID}", Brushes.Orange);
    //        ShowLogMsg(LogType.MainConsole, $"{foundVariables}", Brushes.Orange);

    //        if (foundVariables == 3 && VsmSettings.WebhookSettings.Enabled == true && server.WebhookMessages.Enabled == true)
    //        {
    //            List<string> toSendList = new()
    //            {
    //                !string.IsNullOrEmpty(server.WebhookMessages.ServerReady) ? server.WebhookMessages.ServerReady : "",
    //                (server.WebhookMessages.BroadcastIP == true) ? $"Public IP: {ipAddress}" : "",
    //                (server.WebhookMessages.BroadcastSteamID == true) ? $"SteamID: {steamID}" : ""
    //            };

    //            if (!toSendList.All(x => string.IsNullOrEmpty(x)))
    //            {
    //                string toSend = string.Join("\r", toSendList);
    //                SendDiscordMessage(toSend);
    //            }
    //        }

    //        sr.Close();
    //        fs.Close();
    //    }
    //}

    // 读取服务器日志并处理特定事件
    private async void ReadLog(Server server)
    {
        if (server == null)
        {
            ShowLogMsg(LogType.MainConsole, $"传入的服务器为空！", Brushes.Red);
            return;
        }

        if (server.Runtime?.Process == null)
        {
            ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] 服务器进程未启动，无法读取日志", Brushes.Red);
            return;
        }

        string logPath = Path.Combine(server.Path, "logs", "VRisingServer.log");
        string ipAddress = "";
        string steamID = "";
        int foundVariables = 0;
        // 异步流标志位
        bool serverAsynchronousShuttingDown = false;

        try
        {
            // 首次启动时检查文件是否存在
            if (!server.LogFileExists)
            {
                if (!File.Exists(logPath))
                {
                    ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] 日志文件不存在，请确保服务器已成功启动过一次", Brushes.Yellow);
                    await Task.Delay(5000);
                    if (!File.Exists(logPath))
                    {
                        ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] 日志文件仍不存在，请手动启动服务器一次", Brushes.Red);
                        return;
                    }
                }

                // 文件存在，设置标志位
                server.LogFileExists = true;
                //ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] 已检测到日志文件：{logPath}", Brushes.Green);
            }

            // 打开文件流（无需再次检查文件存在性）
            using FileStream fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader sr = new StreamReader(fs);

            // 移动到文件末尾，只读取新内容
            fs.Seek(0, SeekOrigin.End);

            // 持续读取日志
            while (!serverAsynchronousShuttingDown && server.Runtime.Process != null)
            {
                string line = await sr.ReadLineAsync();
                if (line != null)
                {
                    // 处理服务器IP
                    if (string.IsNullOrEmpty(ipAddress) && line.Contains("PlatformSystemBase - OnPolicyResponse - Public IP: "))
                    {
                        ipAddress = ExtractValue(line, "Public IP: ", " ");
                        ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] 服务器IP: {ipAddress}", Brushes.Cyan);
                    }

                    // 处理SteamID
                    if (string.IsNullOrEmpty(steamID) && line.Contains("SteamNetworking - Successfully logged in with the SteamGameServer API. SteamID: "))
                    {
                        steamID = ExtractValue(line, "SteamID: ", " ");
                        ShowLogMsg(LogType.MainConsole, $"[{server.vsmServerName}] SteamID: {steamID}", Brushes.Cyan);
                    }

                    // 处理关闭异步流
                    if (line.Contains("Shutting down Asynchronous Streaming"))
                    {
                        serverAsynchronousShuttingDown = true;
                    }

                    // 处理玩家事件
                    ProcessPlayerEvent(line);
                }
                else
                {
                    // 无新内容时短暂等待
                    await Task.Delay(500);
                }
            }

            // 发送服务器就绪通知（如果配置）
            if (!serverAsynchronousShuttingDown && VsmSettings.WebhookSettings.Enabled && server.WebhookMessages.Enabled)
            {
                List<string> toSend = new()
            {
                !string.IsNullOrEmpty(server.WebhookMessages.ServerReady) ? server.WebhookMessages.ServerReady : "",
                server.WebhookMessages.BroadcastIP ? $"Public IP: {ipAddress}" : "",
                server.WebhookMessages.BroadcastSteamID ? $"SteamID: {steamID}" : ""
            };

                if (toSend.Any(x => !string.IsNullOrEmpty(x)))
                {
                    SendDiscordMessage(string.Join("\n", toSend));
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            // 特殊处理：文件突然消失（可能被手动删除）
            server.LogFileExists = false;
            ShowLogMsg(LogType.MainConsole,
                $"[{server.vsmServerName}] 日志文件已被删除，请重启服务器: {ex.Message}",
                Brushes.Red);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole,
                $"[{server.vsmServerName}] 日志处理错误：{ex.Message}",
                Brushes.Red);
        }

        //ShowLogMsg(LogType.MainConsole, $"Public IP：{ipAddress}", Brushes.Orange);
        //ShowLogMsg(LogType.MainConsole, $"Game Server SteamID: {steamID}", Brushes.Orange);

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

        // 初始化玩家更新定时器
        InitializePlayerUpdateTimer();
        // 开始监控玩家活动
        //await MonitorPlayerActivity(server, sr, fs, initialPosition);
    }

    // 监控玩家活动
    private async Task MonitorPlayerActivity(Server server, StreamReader sr, FileStream fs, long initialPosition)
    {
        fs.Seek(0, SeekOrigin.Current);
        long lastPosition = fs.Position;
        while (server.Runtime.Process != null)
        {
            try
            {
                // 检查文件是否被截断(例如日志滚动)
                if (fs.Length < lastPosition)
                {
                    //ShowLogMsg(LogType.MainConsole, "日志文件被重置，重新开始监控", Brushes.Yellow);
                    fs.Seek(0, SeekOrigin.Begin);
                    lastPosition = 0;
                }
                else if (fs.Length > lastPosition)
                {
                    // 有新内容可读取
                    fs.Seek(lastPosition, SeekOrigin.Begin);

                    string line;
                    while ((line = await sr.ReadLineAsync()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            //ShowLogMsg(LogType.MainConsole, $"处理玩家事件: {line}", Brushes.Orange);
                            ProcessPlayerEvent(line);
                            lastPosition = fs.Position;
                        }
                    }
                }
                else
                {
                    // 没有新内容，等待
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                ShowLogMsg(LogType.MainConsole, $"监控玩家活动时出错: {ex.Message}", Brushes.Red);
                await Task.Delay(2000);

                // 重置流位置，避免卡死
                fs.Seek(lastPosition, SeekOrigin.Begin);
            }
        }
    }

    private void ProcessPlayerEvent(string logLine)
    {
        if (logLine.Contains("User") && logLine.Contains("connected as ID"))
        {
            HandlePlayerConnect(logLine);
        }
        else if (logLine.Contains("User") && logLine.Contains("disconnected"))
        {
            HandlePlayerDisconnect(logLine);
        }
        else if (logLine.Contains("NetEndPoint") && logLine.Contains("IsAdmin"))
        {
            HandleAdminGrant(logLine);
        }
    }

    private void HandlePlayerConnect(string logLine)
    {
        try
        {
            string characterName = ExtractValue(logLine, "Character: '", "' connected");
            string netEndpoint = ExtractValue(logLine, "{Steam ", "}") ?? "";
            string steamIdStr = ExtractValue(logLine, "}' '", "', approvedUserIndex");

            if (string.IsNullOrEmpty(characterName) || string.IsNullOrEmpty(steamIdStr))
            {
                ShowLogMsg(LogType.MainConsole, $"连接日志缺少关键信息: {logLine}", Brushes.Orange);
                return;
            }

            if (!ulong.TryParse(steamIdStr, out ulong steamId))
            {
                ShowLogMsg(LogType.MainConsole, $"无效SteamID格式: {steamIdStr}", Brushes.Red);
                return;
            }

            DateTime now = DateTime.Now;

            var player = new VRisingPlayerInfo
            {
                SteamId = steamId,
                CharacterName = characterName,
                NetEndPoint = netEndpoint,
                IsOnline = true,
                LoginTime = now,
                LastStatusTime = now,
                IsAuthenticated = true
            };

            if (_connectedPlayers.TryGetValue(steamId, out var existingPlayer))
            {
                // 累加历史时长
                if (existingPlayer.IsOnline && existingPlayer.LoginTime.HasValue)
                {
                    var lastSession = now - existingPlayer.LoginTime.Value;
                    existingPlayer.TotalPlayTime += lastSession;
                    //ShowLogMsg(LogType.MainConsole, $"玩家重连: {characterName} (SteamID: {steamId})，上次时长: {FormatTimeSpan(lastSession)}", Brushes.Cyan);
                }
                player.TotalPlayTime = existingPlayer.TotalPlayTime;
                player.IsAdmin = existingPlayer.IsAdmin;
            }

            _connectedPlayers[steamId] = player;
            _netEndpointToPlayer[netEndpoint] = player;
            _playerDataManager?.AddOrUpdatePlayer(steamId, player);
            _playerDataManager?.SaveAsync();
            //ShowLogMsg(LogType.MainConsole, $"玩家连接: {characterName} (SteamID: {steamId})", Brushes.Green);

            if (VsmSettings.WebhookSettings.Enabled)
            {
                //SendDiscordMessage($"📥 **玩家上线**\n角色: {characterName}\nSteamID: {steamId}");
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"连接处理失败: {ex.Message}", Brushes.Red);
        }
    }

    private void HandlePlayerDisconnect(string logLine)
    {
        try
        {
            string netEndpoint = ExtractValue(logLine, "{Steam ", "}") ?? "";
            string reason = ExtractValue(logLine, "Reason: ", " ") ?? "未知原因";
            VRisingPlayerInfo player = new VRisingPlayerInfo();
            if (string.IsNullOrEmpty(netEndpoint))
            {
                ShowLogMsg(LogType.MainConsole, $"断开日志缺少NetEndpoint: {logLine}", Brushes.Orange);
                return;
            }
            foreach (var playerdata in _playerDataManager.Players.Values)
            {
                if (!playerdata.NetEndPoint.Contains(netEndpoint))
                {
                    continue;
                }
                else
                {
                    player = _playerDataManager.Players.Values.FirstOrDefault(p => p.NetEndPoint.Contains(netEndpoint));
                    if (player == null)
                    {
                        ShowLogMsg(LogType.MainConsole, $"未找到匹配玩家 (NetEndpoint: {netEndpoint})", Brushes.Orange);
                        return;
                    }
                        ShowLogMsg(LogType.MainConsole, $"玩家连接状态：{player.IsOnline.ToString()}", Brushes.Orange);
                    break;
                }
            }

            if (!player.IsOnline)
            {
                ShowLogMsg(LogType.MainConsole, $"重复断开事件: {player.CharacterName} (SteamID: {player.SteamId})", Brushes.Gray);
                return;
            }

            DateTime now = DateTime.Now;
            player.IsOnline = false;
            player.LogoutTime = now;
            player.SessionDuration = now - player.LoginTime;
            player.TotalPlayTime += player.SessionDuration.Value;
            player.LastStatusTime = now;
            player.DisconnectReason = reason;
            player.NetEndPoint = ""; 

            _connectedPlayers[player.SteamId] = player;
            _playerDataManager?.AddOrUpdatePlayer(player.SteamId, player);
            _playerDataManager?.SaveAsync();
            _netEndpointToPlayer.Remove(netEndpoint);

            ShowLogMsg(LogType.MainConsole,
                $"玩家断开: {player.CharacterName} (SteamID: {player.SteamId})\n" +
                $"本次时长: {FormatTimeSpan(player.SessionDuration.Value)} | " +
                $"总时长: {FormatTimeSpan(player.TotalPlayTime)}",
                Brushes.Yellow);

            if (VsmSettings.WebhookSettings.Enabled)
            {
                //SendDiscordMessage($"📤 **玩家下线**\n角色: {player.CharacterName}\n" + $"SteamID: {player.SteamId}\n" + $"本次时长: {FormatTimeSpan(player.SessionDuration.Value)}");
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"断开处理失败: {ex.Message}", Brushes.Red);
        }
    }

    private void HandleAdminGrant(string logLine)
    {
        try
        {
            string steamIdStr = ExtractValue(logLine, "PlatformId: ", " UserIndex:");
            if (string.IsNullOrEmpty(steamIdStr) || !ulong.TryParse(steamIdStr, out ulong steamId))
            {
                ShowLogMsg(LogType.MainConsole, $"无效管理员SteamID: {steamIdStr}", Brushes.Red);
                return;
            }

            if (_connectedPlayers.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = true;
                player.LastStatusTime = DateTime.Now;
                _connectedPlayers[steamId] = player;
                _playerDataManager?.AddOrUpdatePlayer(steamId, player);
                _playerDataManager?.SaveAsync();

                //ShowLogMsg(LogType.MainConsole, $"管理员权限授予: {player.CharacterName} (SteamID: {steamId})", Brushes.Purple);
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"管理员授权处理失败: {ex.Message}", Brushes.Red);
        }
    }


    // 从日志行中提取特定值
    private string ExtractValue(string line, string startMarker, string endMarker)
    {
        int startIndex = line.IndexOf(startMarker);
        if (startIndex == -1)
            return null;

        startIndex += startMarker.Length;
        if (startIndex >= line.Length)
            return null;

        int endIndex = line.IndexOf(endMarker, startIndex);
        if (endIndex == -1)
            return null;

        return line.Substring(startIndex, endIndex - startIndex);
    }

    // 格式化时间间隔
    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{ts.Hours}小时{ts.Minutes}分钟";
        else
            return $"{ts.Minutes}分钟{ts.Seconds}秒";
    }

    // 初始化玩家更新定时器
    private void InitializePlayerUpdateTimer()
    {
        playerUpdateTimer = new System.Timers.Timer(5000); // 每5秒更新一次
        playerUpdateTimer.Elapsed += (sender, e) => UpdatePlayerStatus();
        playerUpdateTimer.Start();
    }

    // 更新玩家状态
    private void UpdatePlayerStatus()
    {
        try
        {
            int onlineCount = _playerDataManager.Players.Values.Count(p => p.IsOnline);

            if (VsmSettings.WebhookSettings.Enabled && onlineCount > 0)
            {
                var onlinePlayers = _playerDataManager.Players.Values
                    .Where(p => p.IsOnline && p.LoginTime.HasValue)
                    .Select(p => $"- {p.CharacterName} (游戏时间: {FormatTimeSpan(DateTime.Now - p.LoginTime.Value)})"); 

                var invalidPlayers = _playerDataManager.Players.Values
                    .Where(p => p.IsOnline && !p.LoginTime.HasValue)
                    .Select(p => $"- {p.CharacterName} (游戏时间: 未知)");

                var allPlayers = onlinePlayers.Concat(invalidPlayers);
                string playerList = string.Join("\n", allPlayers);

                //SendDiscordMessage($"👥 **当前在线玩家 ({onlineCount})**\n{playerList}");
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"更新玩家状态时出错: {ex.Message}", Brushes.Orange);
        }
    }

    #region Events
    private async void ServerProcessExited(object sender, EventArgs e, Server server)
    {
        server.Runtime.State = ServerRuntime.ServerState.已停止;
            
        switch (server.Runtime.Process.ExitCode)
        {
            case 1:
                ShowLogMsg(LogType.MainConsole, $"{server.vsmServerName} 崩溃了。", Brushes.Red);
                break;
            case -2147483645:
                ShowLogMsg(LogType.MainConsole, $"{server.vsmServerName} 已中断，代码为‘-2147483645’，端口无法打开时可能会发生这种情况。确保没有其他服务器正在使用相同的端口。", Brushes.Red);
                break;
        }

        server.Runtime.Process = null;

        if (server.Runtime.RestartAttempts >= 3)
        {
            ShowLogMsg(LogType.MainConsole, $"服务器 '{server.vsmServerName}' 已尝试重新启动3次未成功，正在禁用自动重启功能。", Brushes.Red);
            if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.AttemptStart3) && server.WebhookMessages.Enabled == true)
                SendDiscordMessage(server.WebhookMessages.AttemptStart3);
            server.Runtime.RestartAttempts = 0;
            server.AutoRestart = false;
            if(VsmSettings.AppSettings.SaveLogWhenCrash)
            {
                if (LogManager.WriteServerCrashLog(server))
                    ShowLogMsg(LogType.MainConsole, $@" 已创建服务器崩溃日志，请到 {server.Path}\CrashLog下查看。", Brushes.Yellow);
            }
            ShowLogMsg(LogType.MainConsole, $@" 崩溃后自动重启3次失败重新启动服务器中。", Brushes.Lime);
            await Task.Delay(5);
            if (await StartServer(server))
            {
                ShowLogMsg(LogType.MainConsole, $@" 崩溃后重启服务器成功，重新启用自动重启功能。", Brushes.Lime);
                server.AutoRestart = true;
            }
            return;
        }

        if (server.AutoRestart == true && server.Runtime.UserStopped == false)
        {
            server.Runtime.RestartAttempts++;
            if (VsmSettings.WebhookSettings.Enabled == true && !string.IsNullOrEmpty(server.WebhookMessages.ServerCrash) && server.WebhookMessages.Enabled == true)
                SendDiscordMessage(server.WebhookMessages.ServerCrash);
            if (VsmSettings.AppSettings.SaveLogWhenCrash)
            {
                if (LogManager.WriteServerCrashLog(server))
                    ShowLogMsg(LogType.MainConsole, $@" 已创建服务器崩溃日志，请到 {server.Path}\CrashLog下查看。", Brushes.Yellow);
            }
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
                    //AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
//                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
                    //AutoUpdateLoop();
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
                    AutoUpdateTimer.Dispose();
#if DEBUG
                    AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
#else
//                        AutoUpdateTimer = new PeriodicTimer(TimeSpan.FromMinutes(VsmSettings.AppSettings.AutoUpdateInterval));
#endif
                    AutoUpdateLoop();
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

    // 服务器选择变化事件
    private void ServerTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServerTabControl.SelectedItem is Server selectedServer)
        {
            _currentServer = selectedServer;
            InitializeLogWatchers();
            InitializeServerStateListener();


            // 加载当前日志并更新玩家状态
            if (_currentServer.Runtime?.State == ServerRuntime.ServerState.运行中)
            {
                string logPath = Path.Combine(_currentServer.Path, _logTypeToTag[LogType.VRising]);
                if (File.Exists(logPath))
                {
                    UpdateServerStatusUI();    // 更新面板
                }
            }
        }
    }

    #endregion


    #region Buttons
    private async void StartServerButton_Click(object sender, RoutedEventArgs e)
    {
        // 点击后马上禁用按钮
        var button = sender as Button;
        button.IsEnabled = false; 
            
        bool started = false;
        Server server = ((Button)sender).DataContext as Server;

        // 保存目前软件设置
        MainSettings.Save(VsmSettings);

        if (File.Exists(server.Path + @"\start_server_example.bat"))
        {
            if (started = await StartServer(server))
            {
                // 服务器启动完成后重新启用按钮
                button.IsEnabled = true;
            }
        }
        else
        {
            ShowLogMsg(LogType.MainConsole, $"{server.vsmServerName} 服务器启动失败，请到服务器文件夹确认是否下载成功", Brushes.Red);
            return;
        }

        if (started == true)// && VsmSettings.WebhookSettings.Enabled)
            ReadLog(server);
    }

    private async void UpdateServerButton_Click(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        Server server = button.DataContext as Server;

        if (server == null)
        {
            ShowLogMsg(LogType.MainConsole, $"错误：未找到服务器信息", Brushes.Red);
            return;
        }

        TextBlock buttonText = FindVisualChild<TextBlock>(button, "ButtonText");
        if (buttonText == null)
        {
            ShowLogMsg(LogType.MainConsole, $"警告：未找到按钮文本元素", Brushes.Yellow);
            return;
        }

        try
        {
            // 禁用按钮防止重复点击
            button.IsEnabled = false;
            buttonText.Text = "取消更新";

            // 重新启用按钮，允许取消更新
            button.IsEnabled = true;

            bool success = await UpdateGame(server);

            if (success)
            {
                ShowLogMsg(LogType.MainConsole, $"服务器 {server.vsmServerName} 更新成功！", Brushes.Lime);
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"服务器 {server.vsmServerName} 更新失败！", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"更新过程中发生错误：{ex.Message}", Brushes.Red);
        }
        finally
        {
            buttonText.Text = "更新服务器";
            button.IsEnabled = true;
        }
    }

    // 查找指定名称的子元素
    private T FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && typedChild.Name == name)
            {
                return typedChild;
            }

            T result = FindVisualChild<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    private async void StopServerButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Button button = (Button)sender;
            Server server = button.DataContext as Server;

            if (server == null)
            {
                ShowLogMsg(LogType.MainConsole, $"未找到服务器信息，请确认服务器有正常运行过至少一次", Brushes.Red);
                return;
            }

            ShowLogMsg(LogType.MainConsole, $"正在停止服务器：" + server.vsmServerName, Brushes.Yellow);
            bool success = await StopServer(server);
            LogManager.WriteServerCrashLog(server);
            if (success)
            {
                //playerUpdateTimer.Stop();
                ShowLogMsg(LogType.MainConsole, $"已成功停止服务器：" + server.vsmServerName, Brushes.Lime);
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"无法停止服务器：" + server.vsmServerName, Brushes.Red);
            }

        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"{ex.Message.ToString()}", Brushes.Red);
        }

    }
    private async void RestartServerButton_Click(object sender, RoutedEventArgs e)
    {
        Server server = ((Button)sender).DataContext as Server;
        if (VsmSettings.AppSettings.SaveLogWhenCrash)
        {
            LogManager.WriteServerCrashLog(server);
            ShowLogMsg(LogType.MainConsole, $"已备份 {server.vsmServerName} 服务器日志", Brushes.Lime);
        }
        await RestartServer(server);
    }

    private async Task<bool> RestartServer(Server server)
    {
        ShowLogMsg(LogType.MainConsole, $"正在重启服务器：" + server.vsmServerName, Brushes.Yellow);
        try
        {
            bool success = await StopServer(server);
            if (success)
            {
                if (!LogManager.WriteServerCrashLog(server))
                    ShowLogMsg(LogType.MainConsole, $"备份 {server.vsmServerName} 服务器日志失败", Brushes.Red);
                else
                    ShowLogMsg(LogType.MainConsole, $"已备份 {server.vsmServerName} 服务器日志", Brushes.Lime);

                ShowLogMsg(LogType.MainConsole, $"正在启动服务器：" + server.vsmServerName, Brushes.Yellow);

                success = false;

                MainSettings.Save(VsmSettings);
                if (File.Exists(server.Path + @"\start_server_example.bat"))
                    success = await StartServer(server);
                else
                {
                    ShowLogMsg(LogType.MainConsole, $"未找到服务器启动脚本，请检查服务器安装是否有误", Brushes.Red);
                    return false;
                }

                if (success == true && VsmSettings.WebhookSettings.Enabled)
                    ReadLog(server);
                return true;
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"无法停止服务器：" + server.vsmServerName, Brushes.Red);
                LogManager.WriteServerCrashLog(server);
                return false;
            }

        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"重启服务器发生错误：{ex.Message}", Brushes.Red);
            return false;
        }
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
                ShowLogMsg(LogType.MainConsole, "用户取消本次配置语音服务", Brushes.Yellow);
                return;
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
            ShowLogMsg(LogType.MainConsole, $"错误：找不到要删除的选定服务器", Brushes.Red);
            return;
        }
        bool success = await RemoveServer(server);
        if (!success)
            ShowLogMsg(LogType.MainConsole, $"删除服务器时出错，或操作已中止。", Brushes.Red);
        else
            MainSettings.Save(VsmSettings);
    }

    private void ServerSettingsEditorButton_Click(object sender, RoutedEventArgs e)
    {
        var serverSettingsEditor = Application.Current.Windows.OfType<ServerSettingsEditor>().FirstOrDefault();
        if (serverSettingsEditor != null)
        {
            serverSettingsEditor.Activate();
            serverSettingsEditor.Topmost = true;
            serverSettingsEditor.Topmost = false;
        }
        else
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

    private void GameSettingsEditor_Click(object sender, RoutedEventArgs e)
    {
        var gameSettingsEditor = Application.Current.Windows.OfType<GameSettingsEditor>().FirstOrDefault();
        if (gameSettingsEditor != null)
        {
            gameSettingsEditor.Activate();
            gameSettingsEditor.Topmost = true;
            gameSettingsEditor.Topmost = false;
        }
        else
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

    private async void ManageAdminsButton_Click(object sender, RoutedEventArgs e)
    {
        Server server = ((Button)sender).DataContext as Server;
        var aManager = Application.Current.Windows.OfType<AdminManager>().FirstOrDefault();
        if (aManager != null)
        {   
            aManager.Activate();
            aManager.Topmost = true;
            aManager.Topmost = false;
        }
        else
        {
            if (!File.Exists(server.Path + @"\SaveData\Settings\adminlist.txt"))
            {
                ContentDialog closeFileDialog = new()
                {
                    Content = "找不到管理员文件(adminlist.txt)，请确保服务器安装正确。\n或尝试启动一次服务器",
                    PrimaryButtonText = "是",
                };
                await closeFileDialog.ShowAsync();
                return;
            }
            if (!Application.Current.Windows.OfType<AdminManager>().Any())
            {
                aManager = new AdminManager(server);
                aManager.Show();
            }
        }
    }

    private async void ServerFolderButton_Click(object sender, RoutedEventArgs e)
    {
        Server server = ((Button)sender).DataContext as Server;
        string path = server?.Path;

        try
        {
            if (string.IsNullOrEmpty(path))
            {
                await ShowErrorDialog("路径为空");
                return;
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, ex.Message.ToString(), Brushes.Red);
        }

        if (Directory.Exists(path))
        {
            try
            {
                // 优化 Process.Start 参数，减少开销
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true, 
                    Verb = "open" 
                });
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"打开失败：{ex.Message}");
            }
        }
        else
        {
            await ShowErrorDialog("找不到服务器文件夹。");
        }
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
            
        var modManager = Application.Current.Windows.OfType<ModManager>().FirstOrDefault();
        if (modManager != null)
        {            
            modManager.Activate();
            modManager.Topmost = true;
            modManager.Topmost = false;
        }
        else
        {   
            if ((!Application.Current.Windows.OfType<ModManager>().Any()))
            {
                modManager = new (VsmSettings);
                modManager.Show();
            }
        }
    }

    private void ManagerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var mSettings = Application.Current.Windows.OfType<ManagerSettings>().FirstOrDefault();
        if (mSettings != null)
        {   
            mSettings.Activate();
            mSettings.Topmost = true;
            mSettings.Topmost = false;
        }
        else
        {
            if (!Application.Current.Windows.OfType<ManagerSettings>().Any())
            {
                mSettings = new(VsmSettings);
                mSettings.Show();
            }
        }
    }

    private async void VersionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string latestVersion = await HttpClient.GetStringAsync("https://gitee.com/aGHOSToZero/V-Rising-Server-Manager---Chinese/raw/master/VERSION");
            latestVersion = latestVersion.Trim();

            if (latestVersion != VsmSettings.AppSettings.Version)
            {
                ContentDialog yesNoDialog = new()
                {
                    Content = $"软件有新版本可用于下载，需要关闭软件进行更新，是否更新？\r\r当前版本：{VsmSettings.AppSettings.Version}\r最新版本：{latestVersion}",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };
                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    Process.Start("Update.exe");
                    Application.Current.MainWindow.Close();
                }
                else
                    ShowLogMsg(LogType.MainConsole, $"用户取消了本次软件更新。", Brushes.Yellow);
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"正在运行最新的版本：{latestVersion}", Brushes.Lime);
            }
        }
        catch (Exception ex)
        {
            if (ex.ToString().Contains("不知道这样的主机"))
            {
                ShowLogMsg(LogType.MainConsole, $"搜索软件更新错误：未能到达彼岸，请检查你的网络！", Brushes.Red);
                return;
            }
            else
                ShowLogMsg(LogType.MainConsole, $"搜索软件更新错误：{ex.ToString()}", Brushes.Red);
        }
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

    // 修复工具
    private async void FixTools_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                InstallationProgressBar.Visibility = Visibility.Visible;
                InstallationProgressBar.IsIndeterminate = true; 
                InstallationProgressBar.Value = 0;
            });

            string workingDir = Directory.GetCurrentDirectory();
            string thumbprint = "8da7f965ec5efc37910f1c6e59fdc1cc6a6ede16"; // CA证书指纹

            ShowLogMsg(LogType.MainConsole, "===== 开始证书检查 =====", Brushes.Cyan);
            bool certificateInstalled = await CheckAndInstallCertificate(workingDir, thumbprint);
            ShowLogMsg(LogType.MainConsole, certificateInstalled ? "证书检查完成：已安装或无需安装" : "证书检查完成：未安装（用户取消）", Brushes.Lime);

            if (!certificateInstalled)
            {
                ShowLogMsg(LogType.MainConsole, "===== 修复工具已终止 =====", Brushes.Yellow);
                return; 
            }

            ShowLogMsg(LogType.MainConsole, "===== 开始VC++ Runtime处理 =====", Brushes.Cyan);
            bool vcRuntimeInstalled = await CheckAndInstallVCRuntime(workingDir);
            ShowLogMsg(LogType.MainConsole, vcRuntimeInstalled ?
                "VC++ Runtime处理完成：已安装或安装成功" :
                "VC++ Runtime处理完成：安装失败", vcRuntimeInstalled ? Brushes.Lime : Brushes.Red);

            ShowLogMsg(LogType.MainConsole, "===== 开始DirectX处理 =====", Brushes.Cyan);
            bool directxInstalled = await CheckAndInstallDirectX(workingDir);
            ShowLogMsg(LogType.MainConsole, directxInstalled ? "DirectX处理完成：已安装或安装成功" : "DirectX处理完成：安装失败", directxInstalled ? Brushes.Lime : Brushes.Red);
            
            ShowLogMsg(LogType.MainConsole, "===== 修复工具执行完成 =====", Brushes.Cyan);
            ShowLogMsg(LogType.MainConsole,
                $"证书状态：{(certificateInstalled ? "正常" : "缺失")} | " +
                    $"VC++状态：{(vcRuntimeInstalled ? "正常" : "异常")} | " +
                        $"DirectX状态：{(directxInstalled ? "正常" : "异常")}",
                Brushes.White);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"修复过程出错：{ex.Message}", Brushes.Red);
        }
        finally
        {
            // 隐藏进度条（延迟1秒让用户看到完成状态）
            await Task.Delay(1000);
            Dispatcher.Invoke(() =>
            {
                InstallationProgressBar.Visibility = Visibility.Collapsed;
                InstallationProgressBar.IsIndeterminate = false; 
            });
        }
    }

    // 右键菜单：添加为管理员
    private async void AddAsAdminMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedPlayer = PlayerDataGrid.SelectedItem as VRisingPlayerInfo;
        if (selectedPlayer == null)
        {
            await ShowErrorDialog("请先选中一个玩家");
            return;
        }

        // 调用PlayerDataManager的添加方法
        _playerDataManager?.AddAdmin(selectedPlayer.SteamId);

        // 刷新列表显示
        PlayerDataGrid.Items.Refresh();
        ShowLogMsg(LogType.MainConsole,
            $"已添加 {selectedPlayer.CharacterName} 为管理员", Brushes.Purple);
    }


    // 右键菜单：移除管理员
    private async void RemoveAdminMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var selectedPlayer = PlayerDataGrid.SelectedItem as VRisingPlayerInfo;
        if (selectedPlayer == null)
        {
            await ShowErrorDialog("请先选中一个玩家");
            return;
        }
        _playerDataManager?.RemoveAdmin(selectedPlayer.SteamId);

        // 刷新列表显示
        PlayerDataGrid.Items.Refresh();
        ShowLogMsg(LogType.MainConsole,
            $"已移除 {selectedPlayer.CharacterName} 的管理员权限", Brushes.Purple);
    }

    // 右键菜单：刷新玩家列表
    private void RefreshPlayerListMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RefreshAdminStatus();
    }

    // 刷新管理员状态
    private void RefreshAdminStatus()
    {
        if (_playerDataManager == null) return;

        // 同步所有玩家的管理员状态
        foreach (var player in _playerDataManager.Players.Values)
        {
            player.IsAdmin = _playerDataManager.IsAdmin(player.SteamId);
        }
        PlayerDataGrid.Items.Refresh();
    }



    #endregion
    // 检查并安装证书
    private async Task<bool> CheckAndInstallCertificate(string workingDir, string thumbprint)
    {
        using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
        {
            store.Open(OpenFlags.MaxAllowed);
            var fcollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

            if (fcollection.Count == 0)
            {
                var dialog = new ContentDialog
                {
                    Content = "没有AmazonRootCA1证书，是否导入？",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否",
                    Owner = this
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    ShowLogMsg(LogType.MainConsole, "用户取消证书安装", Brushes.Yellow);
                    return false;
                }

                string caCertPath = Path.Combine(workingDir, "AmazonRootCA1.cer");
                if (!File.Exists(caCertPath))
                {
                    ShowLogMsg(LogType.MainConsole, $"证书文件不存在：{caCertPath}", Brushes.Red);
                    return false;
                }

                try
                {
                    ShowLogMsg(LogType.MainConsole, "开始安装证书...", Brushes.Lime);
                    var caCert = new X509Certificate2(caCertPath);
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(caCert);
                    ShowLogMsg(LogType.MainConsole, "证书安装成功", Brushes.Lime);
                    return true;
                }
                catch (Exception ex)
                {
                    ShowLogMsg(LogType.MainConsole, $"证书安装失败：{ex.Message}", Brushes.Red);
                    return false;
                }
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, "AmazonRootCA1证书已存在，无需安装", Brushes.Lime);
                return true;
            }
        }
    }

    // 检查并安装VC++ Runtime
    private async Task<bool> CheckAndInstallVCRuntime(string workingDir)
    {
        if (CheckVCRuntimeInstalled())
        {
            ShowLogMsg(LogType.MainConsole, "VC++ Runtime已安装，跳过操作", Brushes.Lime);
            return true;
        }
        // 下载安装包
        string installerPath = Path.Combine(workingDir, "vc_redist.x64.exe");
        try
        {
            ShowLogMsg(LogType.MainConsole, "VC++ Runtime未安装，开始下载安装包...", Brushes.Lime);
            using var client = new HttpClient();
            byte[] fileBytes = await client.GetByteArrayAsync(@"https://aka.ms/vs/17/release/vc_redist.x64.exe");
            await File.WriteAllBytesAsync(installerPath, fileBytes);
            ShowLogMsg(LogType.MainConsole, "VC++ Runtime安装包下载完成", Brushes.Lime);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"VC++ Runtime下载失败：{ex.Message}", Brushes.Red);
            return false;
        }

        try
        {
            ShowLogMsg(LogType.MainConsole, "开始安装VC++ Runtime...", Brushes.Lime);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync(); // 等待安装完成（不实时报告进度）

            if (process.ExitCode == 0)
            {
                ShowLogMsg(LogType.MainConsole, "VC++ Runtime安装成功", Brushes.Lime);
                return true;
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"VC++ Runtime安装失败，退出代码：{process.ExitCode}", Brushes.Red);
                return false;
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"VC++ Runtime安装出错：{ex.Message}", Brushes.Red);
            return false;
        }
        finally
        {
            if (File.Exists(installerPath))
                File.Delete(installerPath);
        }
    }

    // 检查并安装DirectX
    private async Task<bool> CheckAndInstallDirectX(string workingDir)
    {
        int directxVersion = GetDirectXVersion();
        if (directxVersion >= 9)
        {
            ShowLogMsg(LogType.MainConsole, $"DirectX版本满足要求（v{directxVersion}），跳过操作", Brushes.Lime);
            return true;
        }
        string installerPath = Path.Combine(workingDir, "directx_Jun2010_redist.exe");
        string extractDir = Path.Combine(workingDir, "directx_Jun2010_redist");
        try
        {
            ShowLogMsg(LogType.MainConsole, $"DirectX版本过低（v{directxVersion}），开始下载安装包...", Brushes.Lime);
            using var client = new HttpClient();
            byte[] fileBytes = await client.GetByteArrayAsync(
                @"https://download.microsoft.com/download/8/4/a/84a35bf1-dafe-4ae8-82af-ad2ae20b6b14/directx_Jun2010_redist.exe");
            await File.WriteAllBytesAsync(installerPath, fileBytes);
            ShowLogMsg(LogType.MainConsole, "DirectX安装包下载完成", Brushes.Lime);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"DirectX下载失败：{ex.Message}", Brushes.Red);
            return false;
        }
        try
        {
            ShowLogMsg(LogType.MainConsole, "开始解压DirectX安装包...", Brushes.Lime);
            Directory.CreateDirectory(extractDir);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = $"/q /T:\"{extractDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                ShowLogMsg(LogType.MainConsole, $"DirectX解压失败，退出代码：{process.ExitCode}", Brushes.Red);
                return false;
            }
            ShowLogMsg(LogType.MainConsole, "DirectX安装包解压完成", Brushes.Lime);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"DirectX解压出错：{ex.Message}", Brushes.Red);
            return false;
        }
        finally
        {
            if (File.Exists(installerPath))
                File.Delete(installerPath);
        }
        try
        {
            ShowLogMsg(LogType.MainConsole, "开始安装DirectX...", Brushes.Lime);
            string dxSetupPath = Path.Combine(extractDir, "DXSETUP.exe");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = dxSetupPath,
                    Arguments = "/silent",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = extractDir
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                ShowLogMsg(LogType.MainConsole, "DirectX安装成功", Brushes.Lime);
                return true;
            }
            else
            {
                ShowLogMsg(LogType.MainConsole, $"DirectX安装失败，退出代码：{process.ExitCode}", Brushes.Red);
                return false;
            }
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"DirectX安装出错：{ex.Message}", Brushes.Red);
            return false;
        }
        finally
        {
            // 清理解压目录
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, recursive: true);
        }
    }
    private bool CheckVCRuntimeInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
        return key != null && key.GetValue("Version") != null;
    }

    // 使用四种方法检测DirectX版本，至少能确保服务器中存在DirectX
    private int GetDirectXVersion()
    {
        try
        {
            // 通过 DirectX SDK 版本号检测
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DirectX");
            if (key != null)
            {
                var dxVersion = key.GetValue("Version") as string;
                if (!string.IsNullOrEmpty(dxVersion))
                {
                    if (dxVersion.Contains("4.09")) 
                        return 9;
                    if (dxVersion.Contains("4.10")) 
                        return 10;
                    if (dxVersion.Contains("4.11")) 
                        return 11;
                    if (dxVersion.Contains("4.12")) 
                        return 12;
                }
            }
            // 通过 Direct3D 功能级别检测
            try
            {
                using var d3dKey = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Direct3D\Capabilities");

                if (d3dKey != null)
                {
                    foreach (var subKeyName in d3dKey.GetSubKeyNames())
                    {
                        using var adapterKey = d3dKey.OpenSubKey(subKeyName);
                        if (adapterKey != null)
                        {
                            var featureLevel = adapterKey.GetValue("MaxFeatureLevel");
                            if (featureLevel != null)
                            {
                                int level = Convert.ToInt32(featureLevel);
                                if (level >= 0xC00) 
                                    return 12; 
                                if (level >= 0xB10) 
                                    return 11; 
                                if (level >= 0xB00) 
                                    return 11; 
                                if (level >= 0xA00) 
                                    return 10; 
                            }
                        }
                    }
                }
            }
            catch 
            { 

            }

            // 通过 dxdiag 检测（低版本好像不适用）
            try
            {
                string tempFile = Path.GetTempFileName();
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dxdiag",
                        Arguments = $"/t \"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.WaitForExit(5000); // 等待最多5秒

                if (process.HasExited && File.Exists(tempFile))
                {
                    // 等待文件写入完成
                    Thread.Sleep(1000);

                    string content = File.ReadAllText(tempFile);
                    File.Delete(tempFile);

                    if (Regex.IsMatch(content, @"DirectX\s+Version:\s+DirectX\s+12", RegexOptions.IgnoreCase)) return 12;
                    if (Regex.IsMatch(content, @"DirectX\s+Version:\s+DirectX\s+11", RegexOptions.IgnoreCase)) return 11;
                    if (Regex.IsMatch(content, @"DirectX\s+Version:\s+DirectX\s+10", RegexOptions.IgnoreCase)) return 10;
                    if (Regex.IsMatch(content, @"DirectX\s+Version:\s+DirectX\s+9", RegexOptions.IgnoreCase)) return 9;
                }
            }
            catch 
            {
                
            }

            // 通过操作系统版本推断 
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major > 10 || (osVersion.Major == 10 && osVersion.Minor >= 0)) return 12;
            if (osVersion.Major == 6 && osVersion.Minor >= 2) return 11; // Windows 8+
            if (osVersion.Major == 6 && osVersion.Minor >= 1) return 10; // Windows 7

            return 9; // 默认返回9 (最常见的最低版本)
        }
        catch
        {
            return 9;
        }
    }

    // 检查并安装DirectX
    private async Task<bool> CheckAndInstallDirectX(string workingDir, Action<int, string> progressCallback)
    {
        progressCallback?.Invoke(0, $"检测DirectX安装状态...");

        int directxVersion = GetDirectXVersion();
        if (directxVersion >= 10)
        {
            progressCallback?.Invoke(100, $"DirectX版本满足要求 (≥ 9)");
            return true;
        }

        progressCallback?.Invoke(10, $"DirectX版本过低，准备更新...");

        string installerPath = Path.Combine(workingDir, "directx_Jun2010_redist.exe");
        string extractDir = Path.Combine(workingDir, "directx_Jun2010_redist");

        try
        {
            if (!File.Exists(installerPath))
            {
                progressCallback?.Invoke(20, $"开始下载DirectX安装程序...");

                using var client = new HttpClient();
                byte[] fileBytes = await client.GetByteArrayAsync(
                    @"https://download.microsoft.com/download/8/4/a/84a35bf1-dafe-4ae8-82af-ad2ae20b6b14/directx_Jun2010_redist.exe");

                using var fileStream = new FileStream(installerPath, FileMode.Create);
                long totalBytes = fileBytes.Length;
                int bufferSize = 8192;
                int bytesWritten = 0;

                for (int i = 0; i < fileBytes.Length; i += bufferSize)
                {
                    int length = Math.Min(bufferSize, fileBytes.Length - i);
                    await fileStream.WriteAsync(fileBytes, i, length);
                    bytesWritten += length;

                    int downloadProgress = 20 + (int)((bytesWritten / (double)totalBytes) * 20);
                    progressCallback?.Invoke(downloadProgress, $"下载中: {downloadProgress - 20}%");
                }

                progressCallback?.Invoke(40, $"下载完成");
            }

            if (!Directory.Exists(extractDir))
            {
                progressCallback?.Invoke(50, $"开始解压DirectX安装程序...");
                Directory.CreateDirectory(extractDir);

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = installerPath,
                        Arguments = $"/q /T:\"{extractDir}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    progressCallback?.Invoke(60, $"解压完成");
                    File.Delete(installerPath);
                }
                else
                {
                    progressCallback?.Invoke(100, $"解压失败，退出代码：{process.ExitCode}");
                    return false;
                }
            }

            string dxSetupPath = Path.Combine(extractDir, "DXSETUP.exe");
            if (File.Exists(dxSetupPath))
            {
                progressCallback?.Invoke(70, $"开始安装DirectX...");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = dxSetupPath,
                        Arguments = "/silent",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = extractDir
                    }
                };

                process.Start();

                int installProgress = 70;
                while (!process.HasExited)
                {
                    await Task.Delay(1000);
                    installProgress = Math.Min(90, installProgress + 5);
                    progressCallback?.Invoke(installProgress, $"安装中... {installProgress}");
                }

                if (process.ExitCode == 0)
                {
                    progressCallback?.Invoke(100, $"DirectX安装成功");
                    return true;
                }
                else
                {
                    progressCallback?.Invoke(100, $"安装失败，退出代码：{process.ExitCode}");
                    return false;
                }
            }
            else
            {
                progressCallback?.Invoke(100, $"找不到DXSETUP.exe");
                return false;
            }
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke(100, $"DirectX安装过程出错：{ex.Message}");
            return false;
        }
    }

    private void ChangeSaveFile_Click(object sender, RoutedEventArgs e)
    {
        Server server = ((Button)sender).DataContext as Server;

        if (VsmSettings.AppSettings.SaveLogWhenCrash)
        {
            if (LogManager.WriteServerCrashLog(server))
                ShowLogMsg(LogType.MainConsole, $@"已创建服务器崩溃日志，请到 {server.Path}\CrashLog下查看。", Brushes.Yellow);
        }
    }

    private void RefreshServerStatus_Click(object sender, RoutedEventArgs e)
    {
        if (_currentServer == null)
        {
            ShowLogMsg(LogType.MainConsole, "请先选择服务器", Brushes.Yellow);
            return;
        }

        string logPath = Path.Combine(_currentServer.Path, _logTypeToTag[LogType.VRising]);
        if (!File.Exists(logPath))
        {
            ShowLogMsg(LogType.MainConsole, "未找到日志文件，无法刷新状态", Brushes.Red);
            return;
        }

        // 手动刷新玩家状态
        UpdateServerStatusUI();
        UpdatePlayerStatus();
        RefreshAdminStatus();
    }

    /// <summary>
    /// 生成时间戳字符串
    /// </summary>
    /// <param name="format" 时间戳格式/>
    /// <returns>格式化后的时间戳字符串</returns>
    public static string GetTimestamp(string format = "file")
    {
        DateTime now = DateTime.Now;

        return format.ToLower() switch
        {
            "file" => now.ToString("yyyyMMdd_HHmmss"),
            "log" => now.ToString("yyyy/MM/dd HH:mm:ss"),
            "unix" => ((long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds).ToString(),
            "unix-ms" => ((long)(now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds).ToString(),
            _ => now.ToString(format)
        };
    }

    // 初始化服务器状态面板
    private void InitServerStatusPanel()
    {
        PlayerDataGrid.ItemsSource = _playerDataManager.Players.Values;
        ServerNameText.Text = "未选择服务器";
        PlayerCountText.Text = "0/0";
        ServerStatusText.Text = "未监控";
        LastUpdatedText.Text = "最后更新: 无";
    }

    // 初始化玩家数据管理器
    private void InitializePlayerDataManager(Server currentServer)
    {
        try
        {
            _playerDataManager = new (currentServer, this);

            _playerDataManager.PlayerUpdated += (player) =>
            {
                Dispatcher.Invoke(() =>
                {
                    //PlayerDataGrid.ItemsSource = _playerDataManager.Players;
                    UpdatePlayerCountText(); // 更新在线人数
                    RefreshAdminStatus();
                });
            };

            //ShowLogMsg(LogType.MainConsole, "玩家数据管理器初始化成功", Brushes.Lime);
        }
        catch (Exception ex)
        {
            ShowLogMsg(LogType.MainConsole, $"玩家数据管理器初始化失败: {ex.Message}", Brushes.Red);
        }
    }

    // 更新在线人数文本
    private void UpdatePlayerCountText()
    {
        int onlineCount = _playerDataManager.Players.Values.Count(p => p.IsOnline);

        Dispatcher.Invoke(() => 
        {
            if (PlayerCountText != null)
            {
                PlayerCountText.Text = $"{onlineCount}/{_playerDataManager.Players.Values.Count}";
            }
        });
    }
}
