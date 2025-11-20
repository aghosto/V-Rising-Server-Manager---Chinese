using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static VRisingServerManager.LogManager;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using Newtonsoft.Json.Converters;


namespace VRisingServerManager;
public class PlayerDataManager : IDisposable
{
    private readonly MainWindow _mainWindow; // 主窗口引用（用于日志输出）
    private readonly Server _currentServer;  // 当前服务器实例
    private readonly string _dataFilePath;   // 数据文件路径

    public ObservableConcurrentDictionary<ulong, VRisingPlayerInfo> Players { get; private set; }

    // 玩家数据更新事件
    public event Action<VRisingPlayerInfo> PlayerUpdated;

    // JSON序列化配置（确保自定义类型可正确序列化）
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = 
        { 
            new JsonStringEnumConverter()
        },
    };
    private string AdminListPath => Path.Combine(_currentServer.Path, @"SaveData\Settings\adminlist.txt");
    private HashSet<ulong> _adminSteamIds = new HashSet<ulong>();
    private string BanListPath => Path.Combine(_currentServer.Path, @"SaveData\Settings\banlist.txt");
    private HashSet<ulong> _banSteamIds = new HashSet<ulong>();

    // 新增：日志监听相关成员
    private FileSystemWatcher _logWatcher; // 当前服务器的日志文件监听
    private string _logFilePath; // 日志文件路径（如VRisingServer.log）
    public event Action<string> LogUpdated; // 日志更新事件（传递新日志内容）

    // 构造函数，指定数据文件路径
    public PlayerDataManager(Server server, MainWindow mainWindow)
    {
        // 验证核心依赖不为null
        _currentServer = server ?? throw new ArgumentNullException(nameof(server), "服务器实例不能为null");
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow), "主窗口实例不能为null");

        // 服务器玩家数据文件路径
        _dataFilePath = Path.Combine(_currentServer.Path, "player_data.json");
        Players = new ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>();

        // 初始化日志文件路径（根据服务器实际日志位置调整）
        _logFilePath = Path.Combine(_currentServer.Path, "logs", "VRisingServer.log");

        // 验证环境并加载数据
        EnsureDirectoryAndPermissions();
        LoadServerPlayerData(); 
        LoadAdminList();

        // 初始化日志监听（默认不启动，需手动调用StartLogWatching）
        InitializeLogWatcher();
    }

    // 新增：初始化日志监听
    private void InitializeLogWatcher()
    {
        if (_logWatcher != null)
        {
            _logWatcher.Dispose(); // 释放已有监听
        }

        _logWatcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(_logFilePath),
            Filter = Path.GetFileName(_logFilePath),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = false // 默认禁用，需手动启动
        };

        // 绑定日志文件变化事件
        _logWatcher.Changed += OnLogFileChanged;
    }

    // 新增：启动日志监听
    public void StartLogWatching()
    {
        if (!File.Exists(_logFilePath))
        {
            _mainWindow.ShowLogMsg($"日志文件不存在，无法启动监听：{_logFilePath}", Brushes.Orange);
            return;
        }

        if (_logWatcher == null)
        {
            InitializeLogWatcher(); // 重新初始化（若已释放）
        }

        _logWatcher.EnableRaisingEvents = true;
        _mainWindow.ShowLogMsg($"已开始监听日志文件：{_logFilePath}", Brushes.Lime);
    }


    // 新增：停止日志监听
    public void StopLogWatching()
    {
        if (_logWatcher != null)
        {
            _logWatcher.EnableRaisingEvents = false;
            _mainWindow.ShowLogMsg($"已停止监听日志文件：{_logFilePath}", Brushes.Yellow);
        }
    }


    // 新增：处理日志文件变化
    private void OnLogFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // 日志文件被修改时，读取新增内容
            string newLogContent = ReadNewLogContent();
            if (!string.IsNullOrEmpty(newLogContent))
            {
                // 通过事件通知外部（如MainWindow）更新UI
                LogUpdated?.Invoke(newLogContent);

                // 若需要在PlayerDataManager中直接处理日志（如解析玩家事件），可在此添加逻辑
                // ProcessLogContent(newLogContent);
            }
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"日志监听错误：{ex.Message}", Brushes.Red);
        }
    }


    // 新增：读取日志文件的新增内容（从上一次读取位置开始）
    private long _lastLogPosition = 0; // 记录上次读取到的位置
    private string ReadNewLogContent()
    {
        if (!File.Exists(_logFilePath))
            return null;

        using (var fs = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            // 若文件大小小于上次位置（如日志被截断），重置位置
            if (fs.Length < _lastLogPosition)
            {
                _lastLogPosition = 0;
            }

            // 若没有新增内容，返回空
            if (fs.Length == _lastLogPosition)
            {
                return null;
            }

            // 从上次位置开始读取
            fs.Position = _lastLogPosition;
            using (var sr = new StreamReader(fs))
            {
                string newContent = sr.ReadToEnd();
                _lastLogPosition = fs.Position; // 更新位置
                return newContent;
            }
        }
    }

    // 确保服务器目录存在且有写入权限
    private void EnsureDirectoryAndPermissions()
    {
        try
        {
            string serverDir = _currentServer.Path;

            // 验证目录存在
            if (!Directory.Exists(serverDir))
            {
                Directory.CreateDirectory(serverDir);
                _mainWindow.ShowLogMsg($"创建服务器目录: {serverDir}", Brushes.Orange);
            }

            // 验证写入权限（创建临时文件后立即删除）
            string testFile = Path.Combine(serverDir, "tmp_permission_test.tmp");
            using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose)) { }
            //_mainWindow.ShowLogMsg($"文件写入权限验证通过（路径: {serverDir}）", Brushes.Lime);
        }
        catch (UnauthorizedAccessException ex)
        {
            _mainWindow.ShowLogMsg($"权限不足：无法写入服务器目录，请以管理员身份运行。", Brushes.Red);
            throw;
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"目录验证失败：{ex.Message}", Brushes.Red);
            throw;
        }
    }

    // 加载现有数据，若文件不存在则创建空文件
    public void LoadOrCreateDataFile()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                Players.Clear(); 
                string json = File.ReadAllText(_dataFilePath);

                var players = JsonSerializer.Deserialize<ConcurrentDictionary<ulong, VRisingPlayerInfo>>(json, _jsonOptions);
                if (players != null)
                {
                    foreach (var player in players)
                    {
                        Players.TryAdd(player.Key, player.Value);
                    }
                    //_mainWindow.ShowLogMsg($"从文件加载 {Players.Count} 条玩家数据", Brushes.Lime);
                }
            }
            else
            {
                // 文件不存在，创建空文件
                Players.Clear(); 
                Save(); 
                _mainWindow.ShowLogMsg($"创建新的玩家数据文件：{_dataFilePath}", Brushes.Orange);
            }
        }
        catch (JsonException ex)
        {
            _mainWindow.ShowLogMsg($"数据文件损坏（JSON解析失败）：{ex.Message}，将创建新文件", Brushes.Red);
            Players.Clear();
            Save(); 
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"加载玩家数据失败：{ex.Message}", Brushes.Red);
            Players.Clear();
        }
    }

    /// <summary>
    /// 从文件加载数据
    /// </summary>
    public void LoadServerPlayerData()
    {
        try
        {
            Players.Clear(); // 文件不存在，清空数据
            if (File.Exists(_dataFilePath))
            {
                string jsonData = File.ReadAllText(_dataFilePath);
                var players = JsonSerializer.Deserialize<ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>>(jsonData);
                Players.Clear();
                if (players != null)
                {
                    foreach (var player in players)
                    {
                        Players.TryAdd(player.Key, player.Value);
                    }
                }
                //_mainWindow.ShowLogMsg($"成功加载玩家数据", Brushes.Lime);
            }
            else
            {
                Players.Clear();
            }
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"加载玩家数据失败: {ex.Message}", Brushes.Red);
            Players.Clear();
        }
    }

    // 服务器重启后，重置所有玩家为离线状态
    public void ResetOnlineStatusOnRestart()
    {
        foreach (var player in Players.Values)
        {
            if (player.IsOnline)
            {
                player.IsOnline = false;
                player.LogoutTime = DateTime.Now;

                // 补充未正常登出的会话时长
                if (player.LoginTime.HasValue)
                {
                    player.SessionDuration = DateTime.Now - player.LoginTime.Value;
                    player.TotalPlayTime += player.SessionDuration.Value;
                }
            }
        }
    }

    // 添加或更新玩家数据
    public void AddOrUpdatePlayer(ulong steamId, VRisingPlayerInfo playerInfo)
    {
        if (playerInfo == null)
        {
            _mainWindow.ShowLogMsg("添加失败：玩家信息为空", Brushes.Red);
            return;
        }

        Players.AddOrUpdate(steamId, playerInfo, (key, existing) =>
        {
            existing.CharacterName = playerInfo.CharacterName;
            existing.IsOnline = playerInfo.IsOnline;
            existing.LoginTime = playerInfo.IsOnline ? DateTime.Now : existing.LoginTime; // 上线时刷新登录时间
            existing.LogoutTime = !playerInfo.IsOnline ? DateTime.Now : existing.LogoutTime; // 下线时刷新登出时间
            existing.SessionDuration = playerInfo.SessionDuration;
            existing.TotalPlayTime += playerInfo.SessionDuration ?? TimeSpan.Zero; // 累加会话时长
            existing.IsAdmin = playerInfo.IsAdmin;
            existing.LastStatusTime = DateTime.Now;
            existing.NetEndPoint = playerInfo.NetEndPoint;
            return existing;
        });

        PlayerUpdated?.Invoke(playerInfo);
        Save();
    }


    // 同步保存数据到文件
    public void Save()
    {
        try
        {
            var dataToSave = new ConcurrentDictionary<ulong, VRisingPlayerInfo>(Players);
            string json = JsonSerializer.Serialize(dataToSave, _jsonOptions);

            File.WriteAllText(_dataFilePath, json);
            //_mainWindow.ShowLogMsg($"玩家数据已保存: {_dataFilePath}", Brushes.Green);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"保存失败: {ex.Message}（路径: {_dataFilePath}）", Brushes.Red);
        }
    }

    // 异步保存数据
    public async Task SaveAsync()
    {
        try
        {
            var dataToSave = new ConcurrentDictionary<ulong, VRisingPlayerInfo>(Players);
            string json = JsonSerializer.Serialize(dataToSave, _jsonOptions);

            await File.WriteAllTextAsync(_dataFilePath, json);
            //_mainWindow.ShowLogMsg($"异步保存玩家数据成功: {_dataFilePath}", Brushes.Green);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"异步保存失败: {ex.Message}", Brushes.Red);
        }
    }

    /// <summary>
    /// 获取玩家信息
    /// </summary>
    public VRisingPlayerInfo GetPlayer(ulong steamId)
    {
        if (Players.TryGetValue(steamId, out var player))
            return player;

        return null;
    }

    [Serializable]
    public class VRisingPlayerInfo : INotifyPropertyChanged
    {
        private ulong _steamId {  get; set; }
        public ulong SteamId
        {
            get => _steamId;
            set
            {
                _steamId = value;
                OnPropertyChanged();
            }
        }
        private string _characterName { get; set; } = "";
        public string CharacterName
        {
            get => _characterName;
            set
            {
                _characterName = value;
                OnPropertyChanged();
            }
        }
        private string _netEndPoint { get; set; } = "";
        public string NetEndPoint
        {
            get => _netEndPoint;
            set
            {
                _netEndPoint = value;
                OnPropertyChanged();
            }
        }
        private bool _isOnline {  get; set; }
        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                _isOnline = value;
                OnPropertyChanged();
            }
        }
        [JsonConverter(typeof(LocalDateTimeConverter))]
        private DateTime _lastStatusTime {  get; set; } = DateTime.MinValue;
        public DateTime LastStatusTime
        {
            get => _lastStatusTime;
            set
            {
                _lastStatusTime = value;
                OnPropertyChanged();
            }
        }
        [JsonConverter(typeof(NullableLocalDateTimeConverter))]
        private DateTime? _loginTime {  get; set; } = new DateTime();
        public DateTime? LoginTime
        {
            get => _loginTime;
            set
            {
                _loginTime = value;
                OnPropertyChanged();
            }
        }
        [JsonConverter(typeof(LocalDateTimeConverter))]
        private DateTime? _logoutTime {  get; set; } = null;
        public DateTime? LogoutTime
        {
            get => _logoutTime;
            set
            {
                _logoutTime = value;
                OnPropertyChanged();
            }
        }
        private TimeSpan? _sessionDuration {  get; set; } = TimeSpan.Zero;
        public TimeSpan? SessionDuration
        {
            get => _sessionDuration;
            set
            {
                _sessionDuration = value;
                OnPropertyChanged();
            }
        }
        private string _disconnectReason {  get; set; } = "LeftGame";
        public string DisconnectReason
        {
            get => _disconnectReason;
            set
            {
                _disconnectReason = value;
                OnPropertyChanged();
            }
        }
        private bool _isAdmin {  get; set; } = false;
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                _isAdmin = value;
                OnPropertyChanged();
            }
        }
        private bool _isBaned {  get; set; } = false;
        public bool IsBaned
        {
            get => _isBaned;
            set
            {
                _isBaned = value;
                OnPropertyChanged();
            }
        }
        private bool _isAuthenticated {  get; set; } = false;
        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                _isAuthenticated = value;
                OnPropertyChanged();
            }
        }
        private TimeSpan _totalPlayTime {  get; set; } = TimeSpan.Zero;
        public TimeSpan TotalPlayTime
        {
            get => _totalPlayTime;
            set
            {
                _totalPlayTime = value;
                OnPropertyChanged();
            }
        }

        // INotifyPropertyChanged接口
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 线程安全的可观察字典
    public class ObservableConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, TValue>,
        INotifyCollectionChanged, INotifyPropertyChanged
    {

        public ObservableConcurrentDictionary() { }

        public ObservableConcurrentDictionary(IDictionary<TKey, TValue> dictionary)
            : base(dictionary) 
        {
            RaiseCollectionChanged(NotifyCollectionChangedAction.Reset);
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public new TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            bool isAdd = !ContainsKey(key);
            var oldValue = isAdd ? default : this[key];
            var newValue = base.AddOrUpdate(key, addValue, updateValueFactory);

            OnCollectionChanged(
                isAdd ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Replace,
                newValue,
                oldValue
            );

            OnPropertyChanged(nameof(Count));
            return newValue;
        }

        private void RaiseCollectionChanged(NotifyCollectionChangedAction action, object item = null, object oldItem = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CollectionChanged?.Invoke(this, oldItem != null
                    ? new NotifyCollectionChangedEventArgs(action, item, oldItem)
                    : new NotifyCollectionChangedEventArgs(action, item));
            });
        }

        private void RaisePropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        public new bool TryAdd(TKey key, TValue value)
        {
            if (base.TryAdd(key, value))
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Add, value);
                OnPropertyChanged(nameof(Count));
                return true;
            }
            return false;
        }

        public new bool TryRemove(TKey key, out TValue value)
        {
            if (base.TryRemove(key, out value))
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, value);
                OnPropertyChanged(nameof(Count));
                return true;
            }
            return false;
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, object oldItem = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CollectionChanged?.Invoke(this,
                    oldItem != null
                        ? new NotifyCollectionChangedEventArgs(action, item, oldItem)
                        : new NotifyCollectionChangedEventArgs(action, item)
                );
            });
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
        }
    }

    /// <summary>
    /// 加载管理员列表（从adminlist.txt读取）
    /// </summary>
    public void LoadAdminList()
    {
        _adminSteamIds.Clear(); 

        try
        {
            if (File.Exists(AdminListPath))
            {
                var lines = File.ReadAllLines(AdminListPath);
                foreach (var line in lines)
                {
                    if (ulong.TryParse(line.Trim(), out ulong steamId))
                    {
                        _adminSteamIds.Add(steamId);
                    }
                }

                //_mainWindow.ShowLogMsg($"已加载管理员列表（{_adminSteamIds.Count} 人）", Brushes.Lime);
            }
            else
            {
                _mainWindow.ShowLogMsg($"管理员列表文件不存在，将创建空列表：{AdminListPath}", Brushes.Orange);
                CreateEmptyAdminList();
            }
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"加载管理员列表失败：{ex.Message}", Brushes.Red);
        }
    }


    /// <summary>
    /// 创建空的管理员列表文件（避免后续操作报“文件不存在”）
    /// </summary>
    private void CreateEmptyAdminList()
    {
        try
        {
            var directory = Path.GetDirectoryName(AdminListPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(AdminListPath, string.Empty);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"创建管理员列表文件失败：{ex.Message}", Brushes.Red);
        }
    }


    /// <summary>
    /// 保存管理员列表（覆盖原文件）
    /// </summary>
    /// <param name="adminSteamIds">管理员SteamID集合（ulong类型）</param>
    public void SaveAdminList(HashSet<ulong> adminSteamIds)
    {
        try
        {
            var lines = adminSteamIds.Select(steamId => steamId.ToString()).ToList();
            File.WriteAllLines(AdminListPath, lines);

            _adminSteamIds = new HashSet<ulong>(adminSteamIds);

            //_mainWindow.ShowLogMsg($"管理员列表已保存（{adminSteamIds.Count} 人）", Brushes.Lime);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"保存管理员列表失败：{ex.Message}", Brushes.Red);
        }
    }


    /// <summary>
    /// 检查玩家是否为管理员
    /// </summary>
    public bool IsAdmin(ulong steamId)
    {
        return _adminSteamIds.Contains(steamId);
    }

    /// <summary>
    /// 添加管理员（并保存到文件）
    /// </summary>
    public void AddAdmin(ulong steamId)
    {
        if (!_adminSteamIds.Contains(steamId))
        {
            _adminSteamIds.Add(steamId);
            SaveAdminList(_adminSteamIds);

            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = true;
                PlayerUpdated?.Invoke(player);
            }
        }
    }

    /// <summary>
    /// 移除管理员（并保存到文件）
    /// </summary>
    public void RemoveAdmin(ulong steamId)
    {
        if (_adminSteamIds.Contains(steamId))
        {
            _adminSteamIds.Remove(steamId);
            SaveAdminList(_adminSteamIds);

            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = false;
                PlayerUpdated?.Invoke(player);
            }
        }
    }

    /// <summary>
    /// 获取所有管理员SteamID
    /// </summary>
    public HashSet<ulong> GetAllAdmins()
    {
        return new HashSet<ulong>(_adminSteamIds);
    }

    /// <summary>
    /// 加载封禁人员列表（从banlist.txt读取）
    /// </summary>
    public void LoadBanList()
    {
        _banSteamIds.Clear();

        try
        {
            if (File.Exists(BanListPath))
            {
                var lines = File.ReadAllLines(BanListPath);
                foreach (var line in lines)
                {
                    if (ulong.TryParse(line.Trim(), out ulong steamId))
                    {
                        _banSteamIds.Add(steamId);
                    }
                }

                //_mainWindow.ShowLogMsg($"已加载封禁人员列表（{_banSteamIds.Count} 人）", Brushes.Lime);
            }
            else
            {
                _mainWindow.ShowLogMsg($"封禁人员列表文件不存在，将创建空列表：{BanListPath}", Brushes.Orange);
                CreateEmptyAdminList(); // 创建空文件
            }
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"加载封禁人员列表失败：{ex.Message}", Brushes.Red);
        }
    }

    /// <summary>
    /// 创建空的封禁人员列表文件
    /// </summary>
    private void CreateEmptyBanList()
    {
        try
        {
            var directory = Path.GetDirectoryName(BanListPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建空文件
            File.WriteAllText(BanListPath, string.Empty);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"创建封禁人员列表文件失败：{ex.Message}", Brushes.Red);
        }
    }

    /// <summary>
    /// 保存封禁人员列表（覆盖原文件）
    /// </summary>
    /// <param name="banSteamIds">封禁人员SteamID集合（ulong类型）</param>
    public void SaveBanList(HashSet<ulong> banSteamIds)
    {
        try
        {
            var lines = banSteamIds.Select(steamId => steamId.ToString()).ToList();

            File.WriteAllLines(BanListPath, lines);

            _banSteamIds = new HashSet<ulong>(banSteamIds);

            //_mainWindow.ShowLogMsg($"封禁人员列表已保存（{adminSteamIds.Count} 人）", Brushes.Lime);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg($"保存封禁人员列表文件失败：{ex.Message}", Brushes.Red);
        }
    }

    /// <summary>
    /// 检查玩家是否为封禁人员
    /// </summary>
    public bool IsBaned(ulong steamId)
    {
        return _banSteamIds.Contains(steamId);
    }

    /// <summary>
    /// 添加封禁人员（并保存到文件）
    /// </summary>
    public void AddBan(ulong steamId)
    {
        if (!_banSteamIds.Contains(steamId))
        {
            _banSteamIds.Add(steamId);
            SaveAdminList(_banSteamIds); // 自动保存

            // 更新玩家数据中的封禁状态
            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsBaned = true;
                PlayerUpdated?.Invoke(player); // 触发UI更新
            }
        }
    }

    /// <summary>
    /// 移除封禁人员（并保存到文件）
    /// </summary>
    public void RemoveBan(ulong steamId)
    {
        if (_banSteamIds.Contains(steamId))
        {
            _banSteamIds.Remove(steamId);
            SaveAdminList(_banSteamIds); // 自动保存

            // 更新玩家数据中的封禁人员状态
            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = false;
                PlayerUpdated?.Invoke(player); // 触发UI更新
            }
        }
    }

    /// <summary>
    /// 获取所有封禁人员SteamID
    /// </summary>
    public HashSet<ulong> GetAllBaned()
    {
        return new HashSet<ulong>(_banSteamIds);
    }


    // 新增：释放资源（停止监听，避免内存泄漏）
    public void Dispose()
    {
        StopLogWatching();
        _logWatcher?.Dispose();
        _logWatcher = null;
    }

    // 自定义日期转换器（处理DateTime类型）
    public class LocalDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly string _format = "yyyy-MM-dd HH:mm:ss"; // 与写入格式严格一致
        private readonly IFormatProvider _culture = System.Globalization.CultureInfo.InvariantCulture;

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string dateStr = reader.GetString();
            if (string.IsNullOrEmpty(dateStr))
                throw new JsonException("日期字符串为空，无法解析");

            // 强制按指定格式解析（忽略系统文化差异）
            if (DateTime.TryParseExact(
                dateStr,
                _format,
                _culture,
                System.Globalization.DateTimeStyles.None,
                out DateTime result))
            {
                return result;
            }

            // 解析失败时抛出详细错误，方便定位问题
            throw new JsonException($"日期格式错误：'{dateStr}' 无法转换为 '{_format}' 格式");
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // 转换为北京时间（+8时区）并格式化为字符串
            var beijingTime = value.ToUniversalTime().AddHours(8);
            writer.WriteStringValue(beijingTime.ToString(_format, _culture));
        }
    }

    // 自定义可空日期转换器（处理DateTime?类型）
    public class NullableLocalDateTimeConverter : JsonConverter<DateTime?>
    {
        private readonly string _format = "yyyy-MM-dd HH:mm:ss"; // 与写入格式严格一致
        private readonly IFormatProvider _culture = System.Globalization.CultureInfo.InvariantCulture;

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string dateStr = reader.GetString();
            if (string.IsNullOrEmpty(dateStr))
                return null;

            // 强制按指定格式解析
            if (DateTime.TryParseExact(
                dateStr,
                _format,
                _culture,
                System.Globalization.DateTimeStyles.None,
                out DateTime result))
            {
                return result;
            }

            // 解析失败时抛出详细错误
            throw new JsonException($"日期格式错误：'{dateStr}' 无法转换为 '{_format}' 格式");
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                var beijingTime = value.Value.ToUniversalTime().AddHours(8);
                writer.WriteStringValue(beijingTime.ToString(_format, _culture));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

}
