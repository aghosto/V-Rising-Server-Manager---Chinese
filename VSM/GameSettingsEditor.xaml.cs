using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Microsoft.Win32;
using System.Text.Json;
using System.Collections.ObjectModel;
using ModernWpf.Controls;
using VRisingServerManager.Controls;

namespace VRisingServerManager
{
    public partial class GameSettingsEditor : Window
    {
        public GameSettings gameSettings;
        public List<Achievement> fakeAchievements = new List<Achievement>();
        public List<VBloodUnitSetting> fakeVBloodUnits = new List<VBloodUnitSetting>();
        public List<Research> fakeResearch = new List<Research>();
        public JsonSerializerOptions serializerOptions = new JsonSerializerOptions { WriteIndented = true };
        private ObservableCollection<Server> servers;

        public GameSettingsEditor(ObservableCollection<Server> sentServers, bool autoLoad = false, int indexToLoad = -1)
        {
            servers = sentServers;
            gameSettings = new GameSettings();
            DataContext = gameSettings;
            InitializeComponent();
            SetupDefaultSettings();

            if (autoLoad == true && indexToLoad != -1 && servers.Count > 0)
            {
                AutoLoad(indexToLoad);
            }
        }

        private void AutoLoad(int serverIndex)
        {
            string fileToLoad = servers[serverIndex].Path + @"\SaveData\Settings\ServerGameSettings.json";
            if (!File.Exists(fileToLoad))
            {
                _ = new ContentDialog
                {
                    Owner = this,
                    Title = "错误",
                    Content = $"加载失败：{fileToLoad}\n请确保该文件存在。",
                    CloseButtonText = "Ok",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
                return;
            }


            using (StreamReader reader = new(fileToLoad))
            {
                string LoadedJSON = reader.ReadToEnd();
                LoadHandler(LoadedJSON);
            }
        }

        private void SetupDefaultSettings()
        {
            fakeVBloodUnits.Clear();
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "头狼",                  UnitId = -1905691330,   UnitLevel = 16, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "冰霜弓箭手基利",        UnitId = 1124739990,    UnitLevel = 20, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "工头鲁弗斯",            UnitId = 2122229952,    UnitLevel = 20, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "碎石斗士埃罗尔",        UnitId = -2025101517,   UnitLevel = 20, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "甲胄师格雷森",          UnitId = 1106149033,    UnitLevel = 27, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "毁灭者戈雷斯温",        UnitId = 577478542,     UnitLevel = 27, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "混沌弓箭手莉迪亚",      UnitId = 763273073,     UnitLevel = 30, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "腐鼠",                  UnitId = -2039908510,   UnitLevel = 30, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "纵火狂克里夫",          UnitId = 1896428751,    UnitLevel = 30, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "精灵游侠波洛拉",        UnitId = -484556888,    UnitLevel = 35, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "恶熊",                  UnitId = -1391546313,   UnitLevel = 35, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "堕落者尼古拉斯",        UnitId = 153390636,     UnitLevel = 35, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "强盗之王昆西",          UnitId = -1659822956,   UnitLevel = 37, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "裁缝比阿特丽斯",        UnitId = -1942352521,   UnitLevel = 40, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "冰霜使者文森特",        UnitId = -29797003,     UnitLevel = 44, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "太阳祭司克里斯蒂娜",    UnitId = -99012450,     UnitLevel = 44, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "亡灵将军克雷格",        UnitId = -1365931036,   UnitLevel = 44, DefaultUnlocked = false }); 
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "吸血鬼猎人特里斯坦",    UnitId = -1449631170,   UnitLevel = 44, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "暗影祭司琳德拉",        UnitId = 939467639,     UnitLevel = 47, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "阴影之刃贝恩",          UnitId = 613251918,     UnitLevel = 47, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "玻璃匠格雷特尔",        UnitId = 910988233,     UnitLevel = 47, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "黑暗学者玛雅",          UnitId = 1945956671,    UnitLevel = 47, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "地卜师泰拉",            UnitId = -1065970933,   UnitLevel = 50, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "夺目弓箭手梅雷迪斯",    UnitId = 850622034,     UnitLevel = 50, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "雪山骇兽霜喉",          UnitId = 24378719,      UnitLevel = 57, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "民兵队长屋大维",        UnitId = 1688478381,    UnitLevel = 57, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "牧羊人拉齐尔",          UnitId = -680831417,    UnitLevel = 57, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "吸血鬼猎人贾德",        UnitId = -1968372384,   UnitLevel = 57, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "刀锋舞者多米娜",        UnitId = -1101874342,   UnitLevel = 60, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "清理者安格拉姆",        UnitId = 106480588,     UnitLevel = 60, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "工程师齐瓦",            UnitId = 172235178,     UnitLevel = 60, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "蜘蛛女王乌戈拉",        UnitId = -548489519,    UnitLevel = 62, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "流浪老人",              UnitId = 109969450,     UnitLevel = 62, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "巴拉顿公爵",            UnitId = -203043163,    UnitLevel = 64, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "夺魂者福洛特",          UnitId = -1208888966,   UnitLevel = 64, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "狼人首领威尔弗雷德",    UnitId = -1007062401,   UnitLevel = 64, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "被诅咒的铁匠西里尔",    UnitId = 326378955,     UnitLevel = 65, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "监察官马格努斯爵士",    UnitId = -26105228,     UnitLevel = 66, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "元素法师梅尔文",        UnitId = -2013903325,   UnitLevel = 70, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "风翼族长莫里安",        UnitId = 685266977,     UnitLevel = 70, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "品酒师布琼男爵",        UnitId = 192051202,     UnitLevel = 70, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "太阳使者阿扎瑞尔",      UnitId = 114912615,     UnitLevel = 74, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "食人魔骇爪",            UnitId = -1347412392,   UnitLevel = 74, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "亨利·布莱克布鲁博士",  UnitId = 814083983,     UnitLevel = 74, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "编咒师玛特卡",          UnitId = -910296704,    UnitLevel = 77, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "能源主管沃尔塔蒂亚",    UnitId = 2054432370,    UnitLevel = 77, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "“决裂者”暗夜元帅冥河", UnitId = 1112948824,   UnitLevel = 79, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "无瑕者索拉鲁斯",        UnitId = -740796338,    UnitLevel = 80, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "巨兽戈雷库什",          UnitId = -1936575244,   UnitLevel = 83, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "恐怖翼兽",              UnitId = -393555055,    UnitLevel = 83, DefaultUnlocked = false });
            fakeVBloodUnits.Add(new VBloodUnitSetting() { Name = "长子亚当",              UnitId = 1233988687,    UnitLevel = 83, DefaultUnlocked = false });
            VBloodData.ItemsSource = fakeVBloodUnits;
            fakeAchievements.Clear();
            fakeAchievements.Add(new Achievement() { ID = -1770927128,      Name = "收集遗骸",              Count = 1,  Unlocked = false }) ;
            fakeAchievements.Add(new Achievement() { ID = 436375429,        Name = "舞刀弄剑",              Count = 2,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -1400391027,      Name = "掌握魔法",              Count = 3,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -2071097880,      Name = "骨制护甲",              Count = 4,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1695239324,       Name = "向森林进发",            Count = 5,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1502386974,       Name = "碎岩利器",              Count = 6,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1694767961,       Name = "暗影领主",              Count = 7,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -1899098914,      Name = "筑起工事",              Count = 8,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 560247139,        Name = "为猎杀做好准备",        Count = 9,  Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -1995132640,      Name = "觅血",                  Count = 10, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -1434604634,      Name = "图书馆里的第一本书",    Count = 11, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1668809517,       Name = "扩展领地",              Count = 12, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 134993992,        Name = "传送门",                Count = 13, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 334973636,        Name = "建造城堡",              Count = 14, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 606418711,        Name = "古堡领主",              Count = 15, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -892747762,       Name = "仆从",                  Count = 16, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -437605270,       Name = "黑暗军团",              Count = 17, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -2104585843,      Name = "统帅宝座",              Count = 18, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1805684941,       Name = "高耸入云的城堡",        Count = 19, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -699165894,       Name = "夜幕战马",              Count = 20, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1861267375,       Name = "吸血鬼帝国",            Count = 21, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = -327597689,       Name = "灵魂之石",              Count = 22, Unlocked = false });
            fakeAchievements.Add(new Achievement() { ID = 1762480233,       Name = "光明之血",              Count = 23, Unlocked = false });
            AchievementsData.ItemsSource = fakeAchievements;
            fakeResearch.Clear();
            fakeResearch.Add(new Research() { Name = "1 阶段", ID = -495424062,  Unlocked = false });
            fakeResearch.Add(new Research() { Name = "2 阶段", ID = -1292809886, Unlocked = false });
            fakeResearch.Add(new Research() { Name = "3 阶段", ID = -1262194203, Unlocked = false });
            ResearchData.ItemsSource = fakeResearch;
        }

        private async void FileMenuSave_Click(object sender, RoutedEventArgs e)
        {
            gameSettings.UnlockedAchievements.Clear();
            foreach (var item in fakeAchievements)
            {
                if (item.Unlocked == true)
                {
                    gameSettings.UnlockedAchievements.Add(item.ID);
                }
            }
            gameSettings.VBloodUnitSettings.Clear();
            foreach (var vblood in fakeVBloodUnits)
            {
                gameSettings.VBloodUnitSettings.Add(new VBloodUnitSetting() { UnitId = vblood.UnitId, UnitLevel = vblood.UnitLevel, DefaultUnlocked = vblood.DefaultUnlocked });
            }
            gameSettings.UnlockedResearchs.Clear();
            foreach (var research in fakeResearch)
            {
                if (research.Unlocked == true)
                    gameSettings.UnlockedResearchs.Add(research.ID);
            }
            switch (StarterEquipmentCombo.SelectedIndex)
            {
                case 0: 
                    gameSettings.StarterEquipmentId = 0;
                    break;
                case 1:
                    gameSettings.StarterEquipmentId = -387090868;
                    break;
                case 2:
                    gameSettings.StarterEquipmentId = -376135143;
                    break;
                case 3:
                    gameSettings.StarterEquipmentId = -1613823352;
                    break;
                case 4:
                    gameSettings.StarterEquipmentId = -1714743400;
                    break;
                case 5:
                    gameSettings.StarterEquipmentId = -258598606;
                    break;
                case 6:
                    gameSettings.StarterEquipmentId = 1177675846;
                    break;
            }
            switch (StarterResourcesCombo.SelectedIndex)
            {
                case 0:
                    gameSettings.StarterResourcesId = 0;
                    break;
                case 1:
                    gameSettings.StarterResourcesId = -696202180;
                    break;
                case 2:
                    gameSettings.StarterResourcesId = 329280709;
                    break;
                case 3:
                    gameSettings.StarterResourcesId = 481718792;
                    break;
                case 4:
                    gameSettings.StarterResourcesId = 470364250;
                    break;
                case 5:
                    gameSettings.StarterResourcesId = -766909665;
                    break;
                case 6:
                    gameSettings.StarterResourcesId = -2131982548;
                    break;
            }

            if (servers.Count > 0)
            {
                ContentDialog yesNoDialog = new()
                {
                    Content = "是否自动保存到服务器？如果原始文件存在，将创建其备份。",
                    PrimaryButtonText = "是",
                    SecondaryButtonText = "否"
                };

                if (await yesNoDialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    EditorSaveDialog dialog = new(servers)
                    {
                        PrimaryButtonText = "保存",
                        CloseButtonText = "取消"
                    };
                    Server server;

                    if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                    {
                        server = dialog.GetServer();
                        if (!Directory.Exists(server.Path + @"\SaveData\Settings"))
                        {
                            MessageBox.Show("在服务器路径中未找到SaveData文件夹。请确保您已启动服务器一次。", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        if (File.Exists(server.Path + @"\SaveData\Settings\ServerGameSettings.json"))
                            File.Copy(server.Path + @"\SaveData\Settings\ServerGameSettings.json", server.Path + @"\SaveData\Settings\ServerGameSettings.bak", true);

                        string SettingsJSON = JsonSerializer.Serialize(gameSettings, serializerOptions);
                        File.WriteAllText(server.Path + @"\SaveData\Settings\ServerGameSettings.json", SettingsJSON);
                        MessageBox.Show("文件已成功保存到：\n" + server.Path + @"\SaveData\Settings\ServerGameSettings.json");
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            try
            {
                string SettingsJSON = JsonSerializer.Serialize(gameSettings, serializerOptions);
                SaveFileDialog SaveSettingsDialog = new SaveFileDialog
                {
                    Filter = "\"JSON files\"|*.json",
                    DefaultExt = "json",
                    FileName = "ServerGameSettings.json",
                    InitialDirectory = Directory.GetCurrentDirectory()
                };
                if (SaveSettingsDialog.ShowDialog() == true)
                {
                    if (File.Exists(SaveSettingsDialog.FileName))
                    {
                        File.Copy(SaveSettingsDialog.FileName, SaveSettingsDialog.FileName + ".bak", true);
                    }
                    File.WriteAllText(SaveSettingsDialog.FileName, SettingsJSON);
                    MessageBox.Show("文件已成功保存到：\n" + SaveSettingsDialog.FileName);
                }
            }
            catch (Exception)
            {
                throw;
            }            
        }

        private void FileMenuLoad_Click(object sender, RoutedEventArgs e)
        {
            string? FileToLoad = "temp";
            OpenFileDialog OpenSettingsDialog = new OpenFileDialog
            {
                Filter = "\"JSON files\"|*.json",
                DefaultExt = "json",
                FileName = "ServerGameSettings.json",
                InitialDirectory = Directory.GetCurrentDirectory()
            };
            if (OpenSettingsDialog.ShowDialog() == true && FileToLoad != null)
            {
                FileToLoad = OpenSettingsDialog.FileName;
            }
            else
            {
                return;
            }
            using (StreamReader reader = new StreamReader(FileToLoad))
            {
                string LoadedJSON = reader.ReadToEnd();
                LoadHandler(LoadedJSON);
            }
        }

        private void LoadPreset(string Preset)
        {
            //var assembly = Assembly.GetExecutingAssembly();
            //Stream stream = assembly.GetManifestResourceStream("VRisingServerManager.Resources." + Preset);
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VRisingServerManager.Resources." + Preset);
            using (StreamReader reader = new StreamReader(stream))
            {
                string LoadedJSON = reader.ReadToEnd();
                LoadHandler(LoadedJSON);
            }
        }

        private void LoadHandler(string JSON)
        {
            try
            {
                SetupDefaultSettings();
                GameSettings LoadedSettings = JsonSerializer.Deserialize<GameSettings>(JSON);
                if (!int.TryParse(LoadedSettings.GameModeType.ToString(), out int GameModeTypeValue))
                {
                    switch (LoadedSettings.GameModeType.ToString())
                    {
                        case "PvE":
                            LoadedSettings.GameModeType = 0;
                            break;
                        case "PvP":
                            LoadedSettings.GameModeType = 1;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.GameModeType = GameModeTypeValue;
                }
                if (!int.TryParse(LoadedSettings.CastleDamageMode.ToString(), out int CastleDamageModeValue))
                {
                    switch (LoadedSettings.CastleDamageMode.ToString())
                    {
                        case "Never":
                            LoadedSettings.CastleDamageMode = 0;
                            break;
                        case "Always":
                            LoadedSettings.CastleDamageMode = 1;
                            break;
                        case "TimeRestricted":
                            LoadedSettings.CastleDamageMode = 2;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.CastleDamageMode = CastleDamageModeValue;
                }
                if (!int.TryParse(LoadedSettings.SiegeWeaponHealth.ToString(), out int SiegeWeaponHealthValue))
                {
                    switch (LoadedSettings.SiegeWeaponHealth.ToString())
                    {
                        case "VeryLow":
                            LoadedSettings.SiegeWeaponHealth = 0;
                            break;
                        case "Low":
                            LoadedSettings.SiegeWeaponHealth = 1;
                            break;
                        case "Normal":
                            LoadedSettings.SiegeWeaponHealth = 2;
                            break;
                        case "High":
                            LoadedSettings.SiegeWeaponHealth = 3;
                            break;
                        case "VeryHigh":
                            LoadedSettings.SiegeWeaponHealth = 4;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.SiegeWeaponHealth = SiegeWeaponHealthValue;
                }
                if (!int.TryParse(LoadedSettings.PlayerDamageMode.ToString(), out int PlayerDamageModeValue))
                {
                    switch (LoadedSettings.PlayerDamageMode.ToString())
                    {
                        case "Always":
                            LoadedSettings.PlayerDamageMode = 0;
                            break;
                        case "Restricted":
                            LoadedSettings.PlayerDamageMode = 1;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.PlayerDamageMode = PlayerDamageModeValue;
                }
                if (!int.TryParse(LoadedSettings.CastleHeartDamageMode.ToString(), out int CastleHeartDamageModeValue))
                {
                    switch (LoadedSettings.CastleHeartDamageMode.ToString())
                    {
                        case "CanBeDestroyedOnlyWhenDecaying":
                            LoadedSettings.CastleHeartDamageMode = 0;
                            break;
                        case "CanBeDestroyedByPlayers":
                            LoadedSettings.CastleHeartDamageMode = 1;
                            break;
                        case "CanBeSeizedOrDestroyedByPlayers":
                            LoadedSettings.CastleHeartDamageMode = 2;
                            break;
                    };
                }
                else
                {
                    LoadedSettings.CastleHeartDamageMode = CastleHeartDamageModeValue;
                }
                if (!int.TryParse(LoadedSettings.PvPProtectionMode.ToString(), out int PvPProtectionModeValue))
                {
                    switch (LoadedSettings.PvPProtectionMode.ToString())
                    {
                        case "Disabled":
                            LoadedSettings.PvPProtectionMode = 0;
                            break;
                        case "VeryShort":
                            LoadedSettings.PvPProtectionMode = 1;
                            break;
                        case "Short":
                            LoadedSettings.PvPProtectionMode = 2;
                            break;
                        case "Medium":
                            LoadedSettings.PvPProtectionMode = 3;
                            break;
                        case "Long":
                            LoadedSettings.PvPProtectionMode = 4;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.PvPProtectionMode = PvPProtectionModeValue;
                }
                if (!int.TryParse(LoadedSettings.DeathContainerPermission.ToString(), out int DeathContainerPermissionValue))
                {
                    switch (LoadedSettings.DeathContainerPermission.ToString())
                    {
                        case "Anyone":
                            LoadedSettings.DeathContainerPermission = 0;
                            break;
                        case "ClanMembers":
                            LoadedSettings.DeathContainerPermission = 1;
                            break;
                        case "OnlySelf":
                            LoadedSettings.DeathContainerPermission = 2;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.DeathContainerPermission = DeathContainerPermissionValue;
                }
                if (!int.TryParse(LoadedSettings.RelicSpawnType.ToString(), out int RelicSpawnTypeValue))
                {
                    switch (LoadedSettings.RelicSpawnType.ToString())
                    {
                        case "Unique":
                            LoadedSettings.RelicSpawnType = 0;
                            break;
                        case "Plentiful":
                            LoadedSettings.RelicSpawnType = 1;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.RelicSpawnType = RelicSpawnTypeValue;
                }
                switch (LoadedSettings.StarterEquipmentId)
                {
                    case -387090868:
                        StarterEquipmentCombo.SelectedIndex = 1;
                        break;
                    case -376135143:
                        StarterEquipmentCombo.SelectedIndex = 2;
                        break;
                    case -1613823352:
                        StarterEquipmentCombo.SelectedIndex = 3;
                        break;
                    case -1714743400:
                        StarterEquipmentCombo.SelectedIndex = 4;
                        break;
                    case -258598606:
                        StarterEquipmentCombo.SelectedIndex = 5;
                        break;
                    case 1177675846:
                        StarterEquipmentCombo.SelectedIndex = 6;
                        break;
                    default:
                        StarterEquipmentCombo.SelectedIndex = 0;
                        break;
                }
                switch (LoadedSettings.StarterResourcesId)
                {
                    case -696202180:
                        StarterResourcesCombo.SelectedIndex = 1;
                        break;
                    case 329280709:
                        StarterResourcesCombo.SelectedIndex = 2;
                        break;
                    case 481718792:
                        StarterResourcesCombo.SelectedIndex = 3;
                        break;
                    case 470364250:
                        StarterResourcesCombo.SelectedIndex = 4;
                        break;
                    case -766909665:
                        StarterResourcesCombo.SelectedIndex = 5;
                        break;
                    case -2131982548:
                        StarterResourcesCombo.SelectedIndex = 6;
                        break;
                    default:
                        StarterResourcesCombo.SelectedIndex = 0;
                        break;
                }
                if (!int.TryParse(LoadedSettings.PlayerInteractionSettings.TimeZone.ToString(), out int TimeZoneValue))
                {
                    switch (LoadedSettings.PlayerInteractionSettings.TimeZone.ToString())
                    {
                        case "Local":
                            TimeZoneCombo.SelectedIndex = 0;
                            break;
                        case "UTC":
                            TimeZoneCombo.SelectedIndex = 1;
                            break;
                        case "PST":
                            TimeZoneCombo.SelectedIndex = 2;
                            break;
                        case "EST":
                            TimeZoneCombo.SelectedIndex = 3;
                            break;
                        case "CET":
                            TimeZoneCombo.SelectedIndex = 4;
                            break;
                        case "CST":
                            TimeZoneCombo.SelectedIndex = 5;
                            break;
                    }
                }
                else
                {
                    LoadedSettings.PlayerInteractionSettings.TimeZone = TimeZoneValue;
                }
                foreach (VBloodUnitSetting unit in LoadedSettings.VBloodUnitSettings)
                {
                    foreach (VBloodUnitSetting fakeUnit in fakeVBloodUnits)
                    {
                        if (unit.UnitId == fakeUnit.UnitId)
                        {
                            if (unit.UnitLevel > 0)
                            {
                                fakeUnit.UnitLevel = unit.UnitLevel;
                            }
                            fakeUnit.DefaultUnlocked = unit.DefaultUnlocked;
                        }
                    }
                }
                foreach (int achievement in LoadedSettings.UnlockedAchievements)
                {
                    foreach (Achievement fakeAchievement in fakeAchievements)
                    {
                        if (achievement == fakeAchievement.ID)
                            fakeAchievement.Unlocked = true;
                    }
                }
                foreach (int research in LoadedSettings.UnlockedResearchs)
                {
                    foreach (Research fakeResearch in fakeResearch)
                    {
                        if (research == fakeResearch.ID)
                            fakeResearch.Unlocked = true;
                    }
                }
                gameSettings = LoadedSettings;
                DataContext = gameSettings;
                VBloodData.Items.Refresh();
                AchievementsData.Items.Refresh();
                ResearchData.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        private void FileMenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadPresetButton(object parameter, RoutedEventArgs e)
        {
            string toSend = ((MenuItem)parameter).Tag.ToString();
            LoadPreset(toSend);
        }
    }
}
