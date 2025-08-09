using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SteamServices;

public class SteamUserService
{
    private readonly string _apiKey = ""; // Steam API密钥
    private readonly HttpClient _httpClient;
    private readonly string _saveDirectory; // 保存JSON文件的目录

    public SteamUserService(string apiKey, string saveDirectory = "SteamProfiles")
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey), "需要提供Steam API密钥");
        _httpClient = new HttpClient();

        // 初始化保存目录
        _saveDirectory = saveDirectory;
        if (!Directory.Exists(_saveDirectory))
        {
            Directory.CreateDirectory(_saveDirectory);
        }
    }

    /// <summary>
    /// 通过SteamID获取用户资料并保存为JSON文件
    /// </summary>
    /// <param name="steamId">Steam用户ID</param>
    /// <param name="fileName">自定义文件名（可选，默认使用SteamID）</param>
    /// <returns>是否保存成功</returns>
    public async Task<bool> GetAndSaveUserProfileAsync(ulong steamId, string fileName = null)
    {
        var profile = await GetUserProfileAsync(steamId);
        return await SaveProfileToJson(profile, fileName ?? steamId.ToString());
    }

    /// <summary>
    /// 通过Vanity URL获取用户资料并保存为JSON文件
    /// </summary>
    public async Task<bool> GetAndSaveUserProfileByVanityUrlAsync(string vanityUrl, string fileName = null)
    {
        var profile = await GetUserProfileByVanityUrlAsync(vanityUrl);
        return await SaveProfileToJson(profile, fileName ?? vanityUrl);
    }

    /// <summary>
    /// 获取用户资料（基础方法）
    /// </summary>
    public async Task<SteamUserProfile> GetUserProfileAsync(ulong steamId)
    {
        try
        {
            string url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_apiKey}&steamids={steamId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SteamProfileResponse>(json);
            return result?.Response?.Players?[0];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取Steam用户资料失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 通过Vanity URL获取用户资料（基础方法）
    /// </summary>
    public async Task<SteamUserProfile> GetUserProfileByVanityUrlAsync(string vanityUrl)
    {
        try
        {
            // 先解析Vanity URL获取SteamID
            string resolveUrl = $"http://api.steampowered.com/ISteamUser/ResolveVanityURL/v0001/?key={_apiKey}&vanityurl={vanityUrl}";
            var resolveResponse = await _httpClient.GetAsync(resolveUrl);
            resolveResponse.EnsureSuccessStatusCode();

            string resolveJson = await resolveResponse.Content.ReadAsStringAsync();
            var resolveResult = JsonConvert.DeserializeObject<VanityUrlResponse>(resolveJson);

            if (resolveResult?.Response?.Success != 1)
            {
                Console.WriteLine($"无法解析Vanity URL: {resolveResult?.Response?.Message}");
                return null;
            }

            // 获取用户资料
            return await GetUserProfileAsync(resolveResult.Response.SteamId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"通过Vanity URL获取失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 将用户资料保存为JSON文件
    /// </summary>
    /// <param name="profile">用户资料对象</param>
    /// <param name="baseFileName">文件名（不含扩展名）</param>
    private async Task<bool> SaveProfileToJson(SteamUserProfile profile, string baseFileName)
    {
        if (profile == null)
        {
            Console.WriteLine("无法保存空的用户资料");
            return false;
        }

        try
        {
            // 构建完整文件路径
            string safeFileName = SanitizeFileName(baseFileName); // 处理文件名中的特殊字符
            string filePath = Path.Combine(_saveDirectory, $"{safeFileName}.json");

            // 序列化并保存
            string json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);

            Console.WriteLine($"用户资料已保存至: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存JSON文件失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清理文件名中的特殊字符
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}


// 序列化所需的模型类
public class SteamProfileResponse
{
    [JsonProperty("response")]
    public SteamProfileData Response { get; set; }
}

public class SteamProfileData
{
    [JsonProperty("players")]
    public SteamUserProfile[] Players { get; set; }
}

public class SteamUserProfile
{
    [JsonProperty("steamid")]
    public string SteamId { get; set; } // 64位SteamID

    [JsonProperty("personaname")]
    public string Username { get; set; } // 用户名

    [JsonProperty("profileurl")]
    public string ProfileUrl { get; set; } // 个人资料URL

    [JsonProperty("avatar")]
    public string AvatarSmall { get; set; } // 小尺寸头像

    [JsonProperty("avatarmedium")]
    public string AvatarMedium { get; set; } // 中等尺寸头像

    [JsonProperty("avatarfull")]
    public string AvatarFull { get; set; } // 大尺寸头像

    [JsonProperty("personastate")]
    public int Status { get; set; } // 在线状态（0:离线, 1:在线, 2:忙碌, 3:离开, 4:睡眠, 5:LookingToTrade, 6:LookingToPlay）

    [JsonProperty("realname")]
    public string RealName { get; set; } // 真实姓名（如果设置）

    [JsonProperty("timecreated")]
    public long? AccountCreatedTimestamp { get; set; } // 账号创建时间戳

    // 转换时间戳为DateTime
    public DateTime? AccountCreatedDate =>
        AccountCreatedTimestamp.HasValue
            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(AccountCreatedTimestamp.Value)
            : null;
}

public class VanityUrlResponse
{
    [JsonProperty("response")]
    public VanityUrlData Response { get; set; }
}

public class VanityUrlData
{
    [JsonProperty("success")]
    public int Success { get; set; } // 1:成功, 42:未找到

    [JsonProperty("message")]
    public string Message { get; set; } // 错误信息

    [JsonProperty("steamid")]
    public ulong SteamId { get; set; } // 解析得到的SteamID
}
