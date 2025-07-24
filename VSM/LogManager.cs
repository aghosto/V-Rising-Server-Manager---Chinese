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
    MainWindow mainWindow = Application.Current.MainWindow as MainWindow;

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
        DateTime Today = DateTime.Today;
        DateTime Now = DateTime.Now;

        string NowToday = Today.ToString("yyyy-MM-dd");
        string NowNow = Now.ToString("HH-mm-ss");

        if (!Directory.Exists(server.Path + @"\CrashLog"))
        {
            Directory.CreateDirectory(server.Path + @"\CrashLog");
            mainWindow.ShowLogMsg(LogType.MainConsole, $"无崩溃日志文件夹，正在创建。", Brushes.Yellow);
        }
        //每日日志文件夹，每日唯一
        if (!Directory.Exists(server.Path + $@"\CrashLog\{NowToday}"))
            Directory.CreateDirectory(server.Path + $@"\CrashLog\{NowToday}");

        //每次重启、崩溃日志文件夹，根据时间每次生成
        Directory.CreateDirectory(server.Path + $@"\CrashLog\{NowToday}\{NowNow}");

        //特殊检查是否为mod服务器
        if (Directory.Exists(server.Path + @"\BepinEx"))
        {
            //尝试复制mod服务器的两个日志文件
            try
            {
                File.Copy(server.Path + @"\BepinEx\ErrorLog.log", server.Path + $@"\CrashLog\{NowToday}\{NowNow}\BepinExErrorLog.log");
                File.Copy(server.Path + @"\BepinEx\LogOutput.log", server.Path + $@"\CrashLog\{NowToday}\{NowNow}\BepinExLogOutput.log");
            }
            catch (Exception ex)
            {
                mainWindow.ShowLogMsg(LogType.MainConsole, $"创建BepInEx服务器日志错误：{ex.Message.ToString()}", Brushes.Red);
            }
        }

        //固定位置原版服务器Log文件
        File.Copy(server.Path + $@"\logs\VRisingServer.log", server.Path + $@"\CrashLog\{NowToday}\{NowNow}\VRisingServer.log");
        return true;
    }
}
