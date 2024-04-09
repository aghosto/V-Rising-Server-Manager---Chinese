# V-Rising-Server-Manager---Chinese
夜族崛起服务端管理器 - 简中汉化版
# @作者     aGHOSToZero


## @软件信息
站内作者@Lacyway的VSM项目个人汉化版，集合了一键开服、服务器连接配置和游戏配置管理等各项功能的软件（基本功能都齐全，且仅Windows）。

本项目地址：https://github.com/aghosto/V-Rising-Server-Manager---Chinese

原项目地址：https://github.com/Lacyway/V-Rising-Server-Manager


###### @特别说明
本软件为原项目的个人汉化，原意旨在方便自己开服和管理，感觉软件界面和管理实在不错，所以也发布上来。

已经把用户界面能看到的几乎所有叙述文本都翻译为较为容易理解的简中文本，个人精力有限，或许会有遗漏。

如在使用中有出现问题（翻译遗漏、错误、软件bug等）可以提issue给我，“有幸”的话我看到会去解决一下（很少上），也可直接QQ联系我1812301343。

本项目翻译纯是自己闲的，原意就是方便自己，所以相关的比如C#也是在翻译途中稍微学了下，实际上并不熟练，只能处理一些比较小的问题。

所以有C#开发经验的不妨下载源码尝试下自定义自己的服务端管理器。


## @使用说明
运行软件所需要的工具：
[.NET Runtime 6.0](https://download.visualstudio.microsoft.com/download/pr/5681bdf9-0a48-45ac-b7bf-21b7b61657aa/bbdc43bc7bf0d15b97c1a98ae2e82ec0/windowsdesktop-runtime-6.0.5-win-x64.exe)

## @@下载与安装
下载项目最近一次更新的文件

中文版：https://github.com/aghosto/V-Rising-Server-Manager---Chinese/releases

原版：  https://github.com/Lacyway/V-Rising-Server-Manager/releases

解压缩下载到的文件到你想安装服务器的文件夹，打开解压缩到的文件夹，双击V Rising Server Manager.exe即可启动软件。


## @@使用
## @@@第一次使用
此处【第一次使用】说的是：
1. 没有使用过SteamCMD下载任何服务器。
2. 没有使用过任何方法开过VRising服务器。

第一次使用会弹出创建服务器的窗口（如未弹出则点击上方[添加服务器]选项），输入你想在管理端显示的服务器名称（该名称仅在管理端显示，服务器默认名字为【夜族崛起服务器】）。

点击创建后切换至新的窗口，因为此时还没有游戏服务端，需要点击右方[更新服务器]，会自行启动安装脚本，注意看黑底绿字Console里的提示文字，等待游戏服务器更新成功。

更新完成后点击上方[启动服务器]进行启动，等待30秒后点击停止服务器，此时游戏服务器的各种默认配置文件都已经有了。

点击[服务器文件夹]，即可打开选定服务器的文件夹，其他东西都不用管，我们只需要看[SaveData]和[VRisingSever_Data]这两个文件夹。

打开文件夹[VRisingServer_Data\StreamingAssets\Settings]，这里的两个文件就是默认的服务器连接配置文件和游戏配置文件，不要更改，复制两个文件到[SaveData\Settings]里。

此时[SaveData\Settings]里有四个文件：[adminlist.txt]、[banlist.txt]、[SeverGameSettings.json]、[SeverHostSettings.json]，要是前面两个文本文件没有可以自己创建一下，内容留空。

返回软件，为了方便，我们打开设置，在主要设置中把[自动加载设置文件]的选项勾选上，保存，此时我们点击[游戏属性配置编辑器]和[服务器连接配置编辑器]就会自动加载刚刚保存到[SeverData]的文件。

如果没有进行复制粘那一步则会出现加载错误提示。

## @@@服务器连接配置编辑
点开[服务器连接配置编辑器]，此处服务器名称和存档名称不能改，服务器名称需要到[SaveData]下的[SeverHostSettings.json]里面改，把引号里的[夜族崛起服务器]改为你想要的名字，存档名称最好不要改除非你知道你在干什么。

回到软件，在这里可以设置你对服务器连接的所有配置，可以把光标悬停在每个框里查看每个设置的功能。

全部设置完成后，点击左上角[文件]->[保存]，后续的选项可以自行选择是否自动保存以及保存到指定服务器（如果有多个的话）。

## @@@游戏设置编辑
点开[游戏属性配置编辑器]，这里有几乎所有游戏内的设置，同样可以用光标悬浮的方式查看每个功能的作用。想偷懒的可以点左上角[预设]导入预设文件，然后直接保存到想要应用的服务器。

也可以对每一项进行更改，有一些功能作者未开发完成/游戏不支持该功能，需要自己注意选择开不开，有关数字的框都可以自行输入以进行更精细的调整，注意配置完成后点左上角进行保存。

## @@@服务器游戏管理员(GM)设置
点击[GM管理]，把需要添加的管理员的64位SteamID添加进去就OK


所有设置配置完成后即可启动服务器。

### @远程控制(RCON)
要使用这个功能，需要在[服务器连接配置编辑器]中把[RCON启用]勾选上，并且设定好密码，端口按需更改。

然后主界面的[绑定到IP]勾选上，点击主界面[远程控制(RCON)]，进入设置，IP和端口不用改（前面端口没改的话），密码输入你刚刚所设定的，点击连接即可。

这个游戏的RCON只有两个指令，就是announce（公告）和announcerestart（重启公告），且公告只能发英文，重启公告只能发数字，但重启公告会用客户端本地的语言给用户提示重启信息。

如果有多个云服务器，且对方设置完成，可以把RCON终端中的IP地址和端口还有密码都改为另外的服务器配置。

也可以使用别的RCON软件进行远程控制，这里有个国内@zkhssb在Github的开源项目可以使用：https://github.com/zkhssb/NectarRCON

#### @致开发者
如果你想自己进行开发（自定义等操作），可以下载源码后在自己的电脑上配置并运行
所需要的工具有：

[.NET 6.0 SDK]：https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-6.0.28-windows-x64-installer
[Visual Studio 22]：https://visualstudio.microsoft.com/zh-hans/thank-you-downloading-visual-studio/?sku=Community&channel=Release&version=VS2022&source=VSLandingPage&cid=2030&passive=false

Tips：最好用VS，不要VS Code，我被.NET 8.202搞崩溃了要

所用库：
- source-rcon-server
- ModernWPF

## 图片
这是原作者使用软件的一些截图
<img src="https://raw.githubusercontent.com/Lacyway/V-Rising-Server-Manager/master/Resources/mainwindow.png" width="400">
<img src="https://raw.githubusercontent.com/Lacyway/V-Rising-Server-Manager/master/Resources/adminmanager.png" width="200">
<img src="https://raw.githubusercontent.com/Lacyway/V-Rising-Server-Manager/master/Resources/rconconsole.png" width="400">
<img src="https://raw.githubusercontent.com/Lacyway/V-Rising-Server-Manager/master/Resources/gamesettingseditor.png" width="400">
<img src="https://raw.githubusercontent.com/Lacyway/V-Rising-Server-Manager/master/Resources/serversettingseditor.png" width="200">
