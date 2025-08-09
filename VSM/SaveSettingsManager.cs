using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VRisingServerManager;



public static class SaveSettingsManager
{
    // JSON 序列化配置（格式化输出）
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 从指定路径加载 HostSettings.json
    /// </summary>
    /// <param name="filePath">HostSettings.json 完整路径</param>
    /// <returns>加载后的 ServerSettings 对象</returns>
    public static ServerSettings LoadServerSettings(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("HostSettings 文件不存在", filePath);
            }

            // 读取文件内容并反序列化为 ServerSettings 对象
            string jsonContent = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ServerSettings>(jsonContent, _jsonOptions)
                ?? new ServerSettings(); // 若反序列化失败，返回默认对象
        }
        catch (Exception ex)
        {
            throw new Exception($"加载 HostSettings 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存 ServerSettings 到指定路径（自动创建备份）
    /// </summary>
    /// <param name="settings">要保存的 ServerSettings 对象</param>
    /// <param name="filePath">保存路径</param>
    /// <param name="createBackup">是否创建备份（默认为 true）</param>
    public static void SaveServerSettings(ServerSettings settings, string filePath, bool createBackup = true)
    {
        try
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (createBackup && File.Exists(filePath))
            {
                string backupPath = $"{filePath}.bak";
                File.Copy(filePath, backupPath, overwrite: true);
            }

            string jsonContent = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(filePath, jsonContent);
        }
        catch (Exception ex)
        {
            throw new Exception($"保存 HostSettings 失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 异步显示错误对话框
    /// </summary>
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
}
