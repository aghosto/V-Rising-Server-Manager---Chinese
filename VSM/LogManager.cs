using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace VRisingServerManager;
public class LogManager : Window
{
    private readonly MainWindow _mainWindow;

    public LogManager(MainWindow mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    Server currentServer;


    public enum LogType
    {
        MainConsole,
        VRising,
        BepinExOutput,
        BepinExError,
        BepInExWindow
    }


    /// <summary>
    /// 备份服务器重启、崩溃日志
    /// </summary>
    /// <param name="server"></param>
    /// <returns></returns>
    public bool WriteServerCrashLog(Server server)
    {
        if (server == null)
        {
            ShowLogError("写入崩溃日志失败：服务器实例为null");
            return false;
        }

        if (string.IsNullOrEmpty(server.Path))
        {
            ShowLogError($"写入崩溃日志失败：[{server.vsmServerName ?? "未知服务器"}] 的路径未设置");
            return false;
        }

        try
        {
            string crashLogDir = Path.Combine(server.Path, "CrashLog", DateTime.Today.ToString("yyyy-MM-dd"), DateTime.Now.ToString("HH-mm-ss"));
            Directory.CreateDirectory(crashLogDir);
            
            // 复制BepInEx日志
            if (Directory.Exists(Path.Combine(server.Path, "BepinEx")))
            {
                CopyFileIfExists(Path.Combine(server.Path, "BepinEx", "ErrorLog.log"), Path.Combine(crashLogDir, "BepinExErrorLog.log"));
                CopyFileIfExists(Path.Combine(server.Path, "BepinEx", "LogOutput.log"), Path.Combine(crashLogDir, "BepinExLogOutput.log"));
            }

            //复制服务器核心日志
            CopyFileIfExists(Path.Combine(server.Path, "logs", "VRisingServer.log"), Path.Combine(crashLogDir, "VRisingServer.log"));
            //ShowLogSuccess($"崩溃日志已保存至：{crashLogDir}");
            return true;
        }
        catch (Exception ex)
        {
            ShowLogError($"写入崩溃日志失败：{ex.Message}");
            return false;
        }
    }

    private void CopyFileIfExists(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            try
            {
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            catch (Exception ex)
            {
                ShowLogError($"复制文件失败：{sourcePath} → {destinationPath}，错误：{ex.Message}");
            }
        }
        else
        {
            ShowLogWarning($"文件不存在，跳过复制：{sourcePath}");
        }
    }

    private void ShowLogError(string message)
    {
        _mainWindow?.ShowLogMsg($"{message}", Brushes.Red);
    }

    private void ShowLogWarning(string message)
    {
        _mainWindow?.ShowLogMsg($"{message}", Brushes.Yellow);
    }

    private void ShowLogSuccess(string message)
    {
        _mainWindow?.ShowLogMsg($"{message}", Brushes.Green);
    }
}


