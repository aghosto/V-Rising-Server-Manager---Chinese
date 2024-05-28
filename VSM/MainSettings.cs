﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VRisingServerManager
{
    public class MainSettings : PropertyChangedBase
    {
        private ObservableCollection<Server> _servers = new();
        public ObservableCollection<Server> Servers
        {
            get => _servers;
            set => SetField(ref _servers, value);
        }
        public AppSettings AppSettings { get; set; } = new AppSettings();
        public Webhook WebhookSettings { get; set; } = new Webhook();
        public List<Mod> DownloadedMods { get; set; } = new List<Mod>();

        /// <summary>
        /// Saves the specified <see cref="MainSettings"/> object.
        /// </summary>
        /// <param name="settings">The <see cref="MainSettings"/> object to save.</param>
        public static void Save(MainSettings settings)
        {
            string dir = Directory.GetCurrentDirectory() + @"\VSMSettings.json";
            JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
            string SettingsJSON = JsonSerializer.Serialize(settings, jsonOptions);
            File.WriteAllText(dir, SettingsJSON);
        }

        /// <summary>
        /// Loads a <see cref="MainSettings"/> object from rootdirectory and returns it.
        /// </summary>
        /// <returns>The loaded <see cref="MainSettings"/> object.</returns>
        public static MainSettings Load()
        {
            string dir = Directory.GetCurrentDirectory() + @"\VSMSettings.json";
            if (File.Exists(dir))
            {
                using (StreamReader sr = new(dir, false))
                {
                    string SettingsJSON = sr.ReadToEnd();
                    MainSettings LoadedSettings = JsonSerializer.Deserialize<MainSettings>(SettingsJSON);
                    return LoadedSettings;
                }
            }
            else
            {
                MessageBox.Show("未找到管理器配置文件(VSMSettings.json)，设置未能导入。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                MainSettings DefaultSettings = new MainSettings();
                return DefaultSettings;
            }
            
        }
    }

    /// <summary>
    /// Object containing information about a <see cref="Server"/>.
    /// </summary>
    public class Server : PropertyChangedBase
    {
        public string Name { get; set; } = "夜族崛起服务器";
        private string _path = Directory.GetCurrentDirectory() + @"\Server";
        public string Path
        {
            get => _path;
            set => SetField(ref _path, value);
        }
        public LaunchSettings LaunchSettings { get; set; } = new LaunchSettings();
        public RCONServerSettings RconServerSettings { get; set; } = new RCONServerSettings();
        private bool _autoRestart = false;
        public bool AutoRestart
        {
            get => _autoRestart;
            set => SetField(ref _autoRestart, value);
        }
        private bool _autoStart = false;
        public bool AutoStart
        {
            get => _autoStart;
            set => SetField(ref _autoStart, value);
        }
        private ServerWebhook _webhookMessages = new();
        public ServerWebhook WebhookMessages
        {
            get => _webhookMessages;
            set => SetField(ref _webhookMessages, value);
        }
        [JsonIgnore]
        public ServerRuntime Runtime { get; set; } = new ServerRuntime();
        private bool _bepInExInstalled = false;
        public bool BepInExInstalled
        {
            get => _bepInExInstalled;
            set => SetField(ref _bepInExInstalled, value);
        }
        private string _bepInExVersion = "";
        public string BepInExVersion
        {
            get => _bepInExVersion;
            set => SetField(ref _bepInExVersion, value);
        }
        private List<string> _installedMods = new();
        public List<string> InstalledMods
        {
            get => _installedMods;
            set => SetField(ref _installedMods, value);
        }
    }

    public class ServerWebhook : PropertyChangedBase
    {
        private bool _enabled = false;
        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }
        private string _startServer = "正在启动服务器。";
        public string StartServer
        {
            get => _startServer;
            set => SetField(ref _startServer, value);
        }
        private string _stopServer = "正在关闭服务器。";
        public string StopServer
        {
            get => _stopServer;
            set => SetField(ref _stopServer, value);
        }
        private string _serverReady = "服务器启动成功。";
        public string ServerReady
        {
            get => _serverReady;
            set => SetField(ref _serverReady, value);
        }
        private string _attemptStart3 = "服务器尝试重新启动3次未成功，正在禁用自动重新启动。";
        public string AttemptStart3
        {
            get => _attemptStart3;
            set => SetField(ref _attemptStart3, value);
        }
        private string _serverCrash = "服务器意外停止，正在重新启动。";
        public string ServerCrash
        {
            get => _serverCrash;
            set => SetField(ref _serverCrash, value);
        }
        private bool _broadcastIP = false;
        public bool BroadcastIP
        {
            get => _broadcastIP;
            set => SetField(ref _broadcastIP, value);
        }
        private bool _broadcastSteamID = false;
        public bool BroadcastSteamID
        {
            get => _broadcastSteamID;
            set => SetField(ref _broadcastSteamID, value);
        }
    }

    /// <summary>
    /// Property of <see cref="Server"/> used to track runtime.
    /// </summary>
    public class ServerRuntime : PropertyChangedBase
    {
        public Process? Process { get; set; }
        public bool UserStopped { get; set; } = false;
        public int RestartAttempts { get; set; } = 0;
        public enum ServerState
        {
            Stopped,
            Running,
            Updating
        }
        private ServerState _state = ServerState.Stopped;
        public ServerState State
        {
            get => _state;
            set => SetField(ref _state, value);
        }
    }

    /// <summary>
    /// Property of <see cref="Server"/> used to fetch RCON Settings.
    /// </summary>
    public class RCONServerSettings : PropertyChangedBase
    {
        private bool _enabled = false;
        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }
        private string _ipAddress = "127.0.0.1";
        public string IPAddress
        {
            get => _ipAddress;
            set => SetField(ref _ipAddress, value);
        }
        private string _port = "25575";
        public string Port
        {
            get => _port;
            set => SetField(ref _port, value);
        }
        private string _password = "";
        public string Password
        {
            get => _password;
            set => SetField(ref _password, value);
        }
    }

    /// <summary>
    /// Property of <see cref="Server"/> used to fetch Launch Settings.
    /// </summary>
    public class LaunchSettings
    {
        public string DisplayName { get; set; } = "V Rising Server";
        public string WorldName { get; set; } = "world1";
        public bool BindToIP { get; set; } = false;
        public string BindingIP { get; set; } = "127.0.0.1";
    }

    /// <summary>
    /// Property of <see cref="MainSettings"/> used to for application settings.
    /// </summary>
    public class AppSettings : PropertyChangedBase
    {
        private bool _verifyUpdates = false;
        public bool VerifyUpdates
        {
            get => _verifyUpdates;
            set => SetField(ref _verifyUpdates, value);
        }
        private bool _autoUpdate = false;
        public bool AutoUpdate
        {
            get => _autoUpdate;
            set => SetField(ref _autoUpdate, value);
        }
        private bool _autoUpdateApp = false;
        public bool AutoUpdateApp
        {
            get => _autoUpdateApp;
            set => SetField(ref _autoUpdateApp, value);
        }
        private int _autoUpdateInterval = 60;
        private bool _showSteamWindow = false;
        public bool ShowSteamWindow
        {
            get => _showSteamWindow;
            set => SetField(ref _showSteamWindow, value);
        }
        public int AutoUpdateInterval
        {
            get => _autoUpdateInterval;
            set => SetField(ref _autoUpdateInterval, value);
        }
        private string _lastUpdateTimeUNIX = "";
        public string LastUpdateTimeUNIX
        {
            get => _lastUpdateTimeUNIX;
            set => SetField(ref _lastUpdateTimeUNIX, value);
        }
        private string _lastUpdateTime = "Last Update on Steam: Unknown";
        public string LastUpdateTime
        {
            get => _lastUpdateTime;
            set => SetField(ref _lastUpdateTime, value);
        }
        private string _version = "1.1.0Ch";
        public string Version
        {
            get => _version;
            set => SetField(ref _version, value);
        }
        private bool _darkMode = false;
        public bool DarkMode
        {
            get => _darkMode;
            set => SetField(ref _darkMode, value);
        }
        private bool _autoLoadEditor = false;
        public bool AutoLoadEditor
        {
            get => _autoLoadEditor;
            set => SetField(ref _autoLoadEditor, value);
        }
        private bool _enableModSupport = false;
        public bool EnableModSupport
        {
            get => _enableModSupport;
            set => SetField(ref _enableModSupport, value);
        }
    }

    /// <summary>
    /// Property of <see cref="MainSettings"/> used for webhook settings.
    /// </summary>
    public class Webhook : PropertyChangedBase
    {
        private bool _enabled = false;
        public bool Enabled
        {
            get => _enabled;
            set => SetField(ref _enabled, value);
        }
        public string URL { get; set; } = "";
        private string _updateFound = "该游戏有更新，正在开始自动更新。";
        public string UpdateFound
        {
            get => _updateFound;
            set => SetField(ref _updateFound, value);
        }
        private string _updateWait = "5分钟后服务器关闭(用于更新)。";
        public string UpdateWait
        {
            get => _updateWait;
            set => SetField(ref _updateWait, value);
        }
    }

    public class Mod : PropertyChangedBase
    {
        private bool _downloaded = false;
        public bool Downloaded
        {
            get => _downloaded;
            set => SetField(ref _downloaded, value);
        }
        private string _uuid4 = "";
        public string Uuid4
        {
            get => _uuid4;
            set => SetField(ref _uuid4, value);
        }
        private string _archiveName = "";
        public string ArchiveName
        {
            get => _archiveName;
            set => SetField(ref _archiveName, value);
        }
        private List<string> _fileNames = new();
        public List<string> FileNames
        {
            get => _fileNames;
            set => SetField(ref _fileNames, value);
        }
    }
}

/// <summary>
/// Class to implement INotifyPropertyChanged easily
/// </summary>
public class PropertyChangedBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value,
    [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}