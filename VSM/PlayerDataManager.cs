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
public class PlayerDataManager
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

    // 管理员文件路径
    private string AdminListPath => Path.Combine(_currentServer.Path, @"SaveData\Settings\adminlist.txt");
    private HashSet<ulong> _adminSteamIds = new HashSet<ulong>();


    // 构造函数，指定数据文件路径
    public PlayerDataManager(Server server, MainWindow mainWindow)
    {
        // 验证核心依赖不为null
        _currentServer = server ?? throw new ArgumentNullException(nameof(server), "服务器实例不能为null");
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow), "主窗口实例不能为null");

        // 服务器玩家数据文件路径
        _dataFilePath = Path.Combine(_currentServer.Path, "player_data.json");
        Players = new ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>();

        // 验证环境并加载数据
        EnsureDirectoryAndPermissions();
        LoadOrCreateDataFile(); 
        LoadAdminList(); 
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
                _mainWindow.ShowLogMsg(LogType.MainConsole, $"创建服务器目录: {serverDir}", Brushes.Orange);
            }

            // 验证写入权限（创建临时文件后立即删除）
            string testFile = Path.Combine(serverDir, "tmp_permission_test.tmp");
            using (var fs = File.Create(testFile, 1, FileOptions.DeleteOnClose)) { }
            //_mainWindow.ShowLogMsg(LogType.MainConsole, $"文件写入权限验证通过（路径: {serverDir}）", Brushes.Lime);
        }
        catch (UnauthorizedAccessException ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"权限不足：无法写入服务器目录，请以管理员身份运行。", Brushes.Red);
            throw;
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"目录验证失败：{ex.Message}", Brushes.Red);
            throw;
        }
    }

    // 加载现有数据，若文件不存在则创建空文件
    private void LoadOrCreateDataFile()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                string json = File.ReadAllText(_dataFilePath);

                var baseDict = JsonSerializer.Deserialize<ConcurrentDictionary<ulong, VRisingPlayerInfo>>(json, _jsonOptions);
                if (baseDict != null)
                {
                    foreach (var kvp in baseDict)
                    {
                        Players.TryAdd(kvp.Key, kvp.Value);
                    }
                    _mainWindow.ShowLogMsg(LogType.MainConsole, $"从文件加载 {Players.Count} 条玩家数据", Brushes.Lime);
                }
                else
                {
                    _mainWindow.ShowLogMsg(LogType.MainConsole, "数据文件为空，初始化空集合", Brushes.Yellow);
                }

                // 服务器重启后重置在线状态
                //ResetOnlineStatusOnRestart();
            }
            else
            {
                // 文件不存在，创建空文件
                Save(); // 调用Save()生成空文件
                _mainWindow.ShowLogMsg(LogType.MainConsole, $"创建新的玩家数据文件：{_dataFilePath}", Brushes.Orange);
            }
        }
        catch (JsonException ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"数据文件损坏（JSON解析失败）：{ex.Message}，将创建新文件", Brushes.Red);
            Players.Clear();
            Save(); // 覆盖损坏文件
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"加载玩家数据失败：{ex.Message}", Brushes.Red);
            Players.Clear();
        }
    }

    /// <summary>
    /// 从文件加载数据
    /// </summary>
    //private void LoadServerPlayerData()
    //{
    //    try
    //    {
    //        if (File.Exists(_dataFilePath))
    //        {
    //            string jsonData = File.ReadAllText(_dataFilePath);
    //            var players = JsonSerializer.Deserialize<ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>>(jsonData);
    //            Players = players ?? new ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>();
    //            _mainWindow.ShowLogMsg(LogType.MainConsole, $"成功加载玩家数据", Brushes.Lime);
    //        }
    //        else
    //        {
    //            Players = new ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>();
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        _mainWindow.ShowLogMsg(LogType.MainConsole, $"加载玩家数据失败: {ex.Message}", Brushes.Red);
    //        Players = new ObservableConcurrentDictionary<ulong, VRisingPlayerInfo>();
    //        // 可添加日志记录
    //    }
    //}

    // 服务器重启后，重置所有玩家为离线状态
    private void ResetOnlineStatusOnRestart()
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
            _mainWindow.ShowLogMsg(LogType.MainConsole, "添加失败：玩家信息为空", Brushes.Red);
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
            //_mainWindow.ShowLogMsg(LogType.MainConsole, $"玩家数据已保存: {_dataFilePath}", Brushes.Green);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"保存失败: {ex.Message}（路径: {_dataFilePath}）", Brushes.Red);
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
            //_mainWindow.ShowLogMsg(LogType.MainConsole, $"异步保存玩家数据成功: {_dataFilePath}", Brushes.Green);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole, $"异步保存失败: {ex.Message}", Brushes.Red);
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


    //[Serializable]
    //public class VRisingPlayerInfo
    //{
    //    public ulong SteamId { get; set; }
    //    public string CharacterName { get; set; } = "";
    //    public string NetEndPoint { get; set; } = "";
    //    public bool IsOnline { get; set; } = false;
    //    public DateTime LastStatusTime { get; set; } = DateTime.MinValue;
    //    public DateTime? LoginTime { get; set; } = new DateTime();
    //    public DateTime? LogoutTime { get; set; } = null;
    //    public TimeSpan? SessionDuration { get; set; } = TimeSpan.Zero;
    //    public string DisconnectReason { get; set; } = "LeftGame";
    //    public bool IsAdmin { get; set; } = false;
    //    public bool IsAuthenticated { get; set; } = false;
    //    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;
    //}

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

        // 实现INotifyPropertyChanged接口
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // 线程安全的可观察字典（支持WPF数据绑定和实时更新）
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

        // 重写TryAdd方法，触发集合变更事件
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

        // 重写TryRemove方法，触发集合变更事件
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

        // 触发集合变更事件的辅助方法
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, object oldItem = null)
        {
            // 确保在UI线程触发事件（WPF要求）
            Application.Current.Dispatcher.Invoke(() =>
            {
                CollectionChanged?.Invoke(this,
                    oldItem != null
                        ? new NotifyCollectionChangedEventArgs(action, item, oldItem)
                        : new NotifyCollectionChangedEventArgs(action, item)
                );
            });
        }

        // 触发属性变更事件的辅助方法
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
        _adminSteamIds.Clear(); // 清空现有缓存

        try
        {
            if (File.Exists(AdminListPath))
            {
                // 读取所有行并转换为ulong
                var lines = File.ReadAllLines(AdminListPath);
                foreach (var line in lines)
                {
                    if (ulong.TryParse(line.Trim(), out ulong steamId))
                    {
                        _adminSteamIds.Add(steamId);
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        // 记录无效格式的行（非空且无法转换为ulong）
                        _mainWindow.ShowLogMsg(LogType.MainConsole,
                            $"管理员列表格式错误：{line}（忽略此行）", Brushes.Orange);
                    }
                }

                _mainWindow.ShowLogMsg(LogType.MainConsole,
                    $"已加载管理员列表（{_adminSteamIds.Count} 人）", Brushes.Lime);
            }
            else
            {
                _mainWindow.ShowLogMsg(LogType.MainConsole,
                    $"管理员列表文件不存在，将创建空列表：{AdminListPath}", Brushes.Orange);
                CreateEmptyAdminList(); // 创建空文件
            }
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole,
                $"加载管理员列表失败：{ex.Message}", Brushes.Red);
        }
    }


    /// <summary>
    /// 创建空的管理员列表文件（避免后续操作报“文件不存在”）
    /// </summary>
    private void CreateEmptyAdminList()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(AdminListPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建空文件
            File.WriteAllText(AdminListPath, string.Empty);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole,
                $"创建管理员列表文件失败：{ex.Message}", Brushes.Red);
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
            // 转换为字符串列表（每行一个SteamID）
            var lines = adminSteamIds.Select(steamId => steamId.ToString()).ToList();

            // 写入文件
            File.WriteAllLines(AdminListPath, lines);

            // 更新缓存
            _adminSteamIds = new HashSet<ulong>(adminSteamIds);

            _mainWindow.ShowLogMsg(LogType.MainConsole,
                $"管理员列表已保存（{adminSteamIds.Count} 人）", Brushes.Lime);
        }
        catch (Exception ex)
        {
            _mainWindow.ShowLogMsg(LogType.MainConsole,
                $"保存管理员列表失败：{ex.Message}", Brushes.Red);
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
            SaveAdminList(_adminSteamIds); // 自动保存

            // 更新玩家数据中的管理员状态
            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = true;
                PlayerUpdated?.Invoke(player); // 触发UI更新
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
            SaveAdminList(_adminSteamIds); // 自动保存

            // 更新玩家数据中的管理员状态
            if (Players.TryGetValue(steamId, out var player))
            {
                player.IsAdmin = false;
                PlayerUpdated?.Invoke(player); // 触发UI更新
            }
        }
    }


    /// <summary>
    /// 获取所有管理员SteamID
    /// </summary>
    public HashSet<ulong> GetAllAdmins()
    {
        return new HashSet<ulong>(_adminSteamIds); // 返回副本，避免外部直接修改缓存
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
