using System.Diagnostics;
using System.IO.Compression;

Console.WriteLine("准备下载最新版本。");
// Console.WriteLine("按任意键开始...");
// Console.ReadLine();

Process[] vsmProcesses = Process.GetProcessesByName("VRisingServerManager");
if (vsmProcesses.Length != 0)
{
    Console.WriteLine("VRisingServerManager正在运行中，请在更新之前退出VSM。");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    Environment.Exit(2);
}

string workingDir = AppDomain.CurrentDomain.BaseDirectory;
HttpClient httpClient = new();

if (!File.Exists(workingDir + @"\VRisingServerManager.exe"))
{
    Console.WriteLine("在根目录中找不到‘VRisingServerManager.exe’。请确保应用程序已正确安装。");
    Console.ReadKey();
    Environment.Exit(2);
}

Console.WriteLine("正在开始更新VSM。");
Console.WriteLine("正在下载文件...");

byte[] fileBytes = await httpClient.GetByteArrayAsync(@"https://github.com/aghosto/V-Rising-Server-Manager---Chinese/releases/latest/download/VSM-Ch.zip");
Console.WriteLine("下载成功。");

if (Directory.Exists(workingDir + @"\temp"))
    Directory.Delete(workingDir + @"\temp", true);

Directory.CreateDirectory(workingDir + @"\temp");

await File.WriteAllBytesAsync(workingDir + @"\temp\VSM.zip", fileBytes);

Console.WriteLine();

Console.WriteLine("正在创建设置的备份。");
if (!Directory.Exists(workingDir + @"\Backups"))
    Directory.CreateDirectory(workingDir + @"\Backups");

if (File.Exists(workingDir + @"\VSMSettings.json"));
    File.Copy(workingDir + @"\VSMSettings.json", workingDir + @"\Backups\VSMSettings.bak", true);

Console.WriteLine();

ZipFile.ExtractToDirectory(workingDir + @"\temp\VSM.zip", workingDir + @"\temp", true);

Console.WriteLine("正在复制新文件...");
string[] files = Directory.GetFiles(workingDir + @"\temp");

foreach (string file in files)
{
    string fileName = Path.GetFileName(file);    
    if (fileName != "VSMUpdater.exe" && fileName != "VSM.zip")
    {
        Console.WriteLine("Copying: " + fileName);
        File.Copy(file, workingDir + fileName, true);
    }
}

if (Directory.Exists(workingDir + @"\temp"))
    Directory.Delete(workingDir + @"\temp", true);

Console.WriteLine();
Console.WriteLine(@"更新完成。VMS设置的备份可以在\Backups文件夹中找到。");
Console.WriteLine("按任意键关闭窗口并启动软件...");
Console.ReadKey();

Process.Start("VRisingServerManager.exe");

Environment.Exit(0);