using System.Collections.Generic;
using System.Text.Json.Serialization;
using static System.Windows.Forms.DataFormats;

namespace VRisingServerManager
{

    public class EmptyJSON
    {

    }

    public class CastleStatModifiersGlobal
    {
        public double TickPeriod { get; set; } = 5.0;
        public double DamageResistance { get; set; } = 0.0;
        public int SafetyBoxLimit { get; set; } = 1;
        public int EyeStructuresLimit { get; set; } = 1;
        public int TombLimit { get; set; } = 12;
        public int VerminNestLimit { get; set; } = 4;
        public int PrisonCellLimit { get; set; } = 16;
        [JsonIgnore]
        public PylonPenalties PylonPenalties { get; set; } = new PylonPenalties();
        [JsonIgnore]
        public FloorPenalties FloorPenalties { get; set; } = new FloorPenalties();
        public HeartLimits HeartLimits { get; set; } = new HeartLimits();
        public int CastleLimit { get; set; } = 2;
        public int NetherGateLimit { get; set; } = 1;
        public int ThroneOfDarknessLimit {  get; set; } = 1;
        public string CastleHeartLimitType { get; set; } = "Clan";

    }

    public class VBloodUnitSetting
    {
        [JsonIgnore]
        public string? Name { get; set; }
        public int UnitId { get; set; } = 1;
        public int? UnitLevel { get; set; }
        public bool DefaultUnlocked { get; set; } = false;
    }

    public class EquipmentStatModifiersGlobal
    {
        public double MaxHealthModifier { get; set; } = 1.0;
        public double ResourceYieldModifier { get; set; } = 1.0;
        public double PhysicalPowerModifier { get; set; } = 1.0;
        public double SpellPowerModifier { get; set; } = 1.0;
    }

    public class FloorPenalties
    {
        public EmptyJSON? Range1 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range2 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range3 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range4 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range5 { get; set; } = new EmptyJSON();
    }

    public class PylonPenalties
    {
        public EmptyJSON? Range1 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range2 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range3 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range4 { get; set; } = new EmptyJSON();
        public EmptyJSON? Range5 { get; set; } = new EmptyJSON();
    }

    public class GameTimeModifiers
    {
        public double DayDurationInSeconds { get; set; } = 1080.00;
        public int DayStartHour { get; set; } = 9;
        public int DayStartMinute { get; set; } = 0;
        public int DayEndHour { get; set; } = 17;
        public int DayEndMinute { get; set; } = 0;
        public int BloodMoonFrequency_Min { get; set; } = 10;
        public int BloodMoonFrequency_Max { get; set; } = 18;
        public double BloodMoonBuff { get; set; } = 0.2;
    }

    public class HeartLimits
    {
        public Level1 Level1 { get; set; } = new Level1();
        public Level2 Level2 { get; set; } = new Level2();
        public Level3 Level3 { get; set; } = new Level3();
        public Level4 Level4 { get; set; } = new Level4();
        public Level5 Level5 { get; set; } = new Level5();
    }

    public class Level1
    {
        public int Level { get; set; } = 1;
        public int FloorLimit { get; set; } = 50;
        public int ServantLimit { get; set; } = 4;
        public int BuildLimits { get; set; } = 2;
        public int HeightLimit { get; set; } = 3;
    }

    public class Level2
    {
        public int Level { get; set; } = 2;
        public int FloorLimit { get; set; } = 140;
        public int ServantLimit { get; set; } = 5;
        public int BuildLimits { get; set; } = 2;
        public int HeightLimit { get; set; } = 3;
    }

    public class Level3
    {
        public int Level { get; set; } = 3;
        public int FloorLimit { get; set; } = 240;
        public int ServantLimit { get; set; } = 6;
        public int BuildLimits { get; set; } = 2;
        public int HeightLimit { get; set; } = 3;
    }

    public class Level4
    {
        public int Level { get; set; } = 4;
        public int FloorLimit { get; set; } = 360;
        public int ServantLimit { get; set; } = 7;
        public int BuildLimits { get; set; } = 2;
        public int HeightLimit { get; set; } = 3;
    }

    public class Level5
    {
        public int Level { get; set; } = 5;
        public int FloorLimit { get; set; } = 420;
        public int ServantLimit { get; set; } = 8;
        public int BuildLimits { get; set; } = 2;
        public int HeightLimit { get; set; } = 3;
    }

    public class PlayerInteractionSettings
    {
        public dynamic TimeZone { get; set; } = 0;
        public VSPlayerWeekdayTime VSPlayerWeekdayTime { get; set; } = new VSPlayerWeekdayTime();
        public VSPlayerWeekendTime VSPlayerWeekendTime { get; set; } = new VSPlayerWeekendTime();
        public VSCastleWeekdayTime VSCastleWeekdayTime { get; set; } = new VSCastleWeekdayTime();
        public VSCastleWeekendTime VSCastleWeekendTime { get; set; } = new VSCastleWeekendTime();
    }    

    public class Range1
    {
        public double? Percentage { get; set; }
        public int? Lower { get; set; }
        public int? Higher { get; set; }
    }

    public class Range2
    {
        public double? Percentage { get; set; }
        public int? Lower { get; set; }
        public int? Higher { get; set; }
    }

    public class Range3
    {
        public double? Percentage { get; set; }
        public int? Lower { get; set; }
        public int? Higher { get; set; }
    }

    public class Range4
    {
        public double? Percentage { get; set; }
        public int? Lower { get; set; }
        public int? Higher { get; set; }
    }

    public class Range5
    {
        public double? Percentage { get; set; }
        public int? Lower { get; set; }
        public int? Higher { get; set; }
    }

    public class UnitStatModifiersGlobal
    {
        public double MaxHealthModifier { get; set; } = 1.0;
        public double PowerModifier { get; set; } = 1.0;
        public int LevelIncrease { get; set; } = 0;
    }

    public class UnitStatModifiersVBlood
    {
        public double MaxHealthModifier { get; set; } = 1.0;
        public double PowerModifier { get; set; } = 1.0;
        public int LevelIncrease { get; set; } = 0;
    }

    public class VampireStatModifiers
    {
        public double MaxHealthModifier { get; set; } = 1.0;
        public double PhysicalPowerModifier { get; set; } = 1.0;
        public double SpellPowerModifier { get; set; } = 1.0;
        public double ResourcePowerModifier { get; set; } = 1.0;
        public double DamageReceivedModifier { get; set; } = 1.0;
        public double ReviveCancelDelay { get; set; } = 5.0;
    }

    public class WarEventGameSettings
    {
        public dynamic Interval { get; set; } = 1;
        public dynamic MinorDuration { get; set; } = 2;
        public dynamic MajorDuration { get; set; } = 2;
        public WeekdayTime WeekdayTime { get; set; } = new WeekdayTime();
        public WeekendTime WeekendTime { get; set; } = new WeekendTime();
        public ScalingPlayers1 ScalingPlayers1 { get; set; } = new ScalingPlayers1();
        public ScalingPlayers2 ScalingPlayers2 { get; set; } = new ScalingPlayers2();
        public ScalingPlayers3 ScalingPlayers3 { get; set; } = new ScalingPlayers3();
        public ScalingPlayers4 ScalingPlayers4 { get; set; } = new ScalingPlayers4();

    }
    public class WeekdayTime
    {
        public int StartHour { get; set; } = 0;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 59;
    }
    public class WeekendTime
    {
        public int StartHour { get; set; } = 0;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 59;
    }
    public class ScalingPlayers1
    {
        public double PointsModifier { get; set; } = 1.00;
        public double DropModifier { get; set; } = 1.00;
    }
    public class ScalingPlayers2
    {
        public double PointsModifier { get; set; } = 0.5;
        public double DropModifier { get; set; } = 0.5;
    }
    public class ScalingPlayers3
    {
        public double PointsModifier { get; set; } = 0.25;
        public double DropModifier { get; set; } = 0.25;
    }
    public class ScalingPlayers4
    {
        public double PointsModifier { get; set; } = 0.25;
        public double DropModifier { get; set; } = 0.25;
    }
    public class VSCastleWeekdayTime
    {
        public int StartHour { get; set; } = 17;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 0;
    }

    public class VSCastleWeekendTime
    {
        public int StartHour { get; set; } = 17;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 0;
    }

    public class VSPlayerWeekdayTime
    {
        public int StartHour { get; set; } = 17;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 0;
    }

    public class VSPlayerWeekendTime
    {
        public int StartHour { get; set; } = 17;
        public int StartMinute { get; set; } = 0;
        public int EndHour { get; set; } = 23;
        public int EndMinute { get; set; } = 0;
    }

    public class TraderModifiers
    {
        public double StockModifier { get; set; } = 1.00;
        public double PriceModifier { get; set; } = 1.00;
        public double RestockTimerModifier { get; set; } = 1.00;
    }

    public class Rcon
    {
        public bool Enabled { get; set; } = false;
        public string Password { get; set; } = "输入密码";
        public int Port { get; set; } = 25575;
    }

    public class API
    {
        public bool Enabled { get; set; } = false;
        public int BindPort { get; set; } = 9090;
    }

    public class GameSettings
    {
        public dynamic GameDifficulty { get; set; } = 1;
        public dynamic GameModeType { get; set; } = 0;
        public dynamic CastleDamageMode { get; set; } = 0;
        public dynamic SiegeWeaponHealth { get; set; } = 2;
        public dynamic PlayerDamageMode { get; set; } = 0;
        public dynamic CastleHeartDamageMode { get; set; } = 0;
        public dynamic PvPProtectionMode { get; set; } = 2;
        public dynamic DeathContainerPermission { get; set; } = 0;
        public dynamic RelicSpawnType { get; set; } = 0;
        public bool CanLootEnemyContainers { get; set; } = true;
        public bool BloodBoundEquipment { get; set; } = true;
        public bool TeleportBoundItems { get; set; } = true;
        public bool BatBoundItems { get; set; } = true;
        public bool AllowGlobalChat { get; set; } = true;
        public bool AllWaypointsUnlocked { get; set; } = false;
        public bool FreeCastleRaid { get; set; } = false;
        public bool FreeCastleClaim { get; set; } = false;
        public bool FreeCastleDestroy { get; set; } = false;        
        public bool CastleRelocationEnabled { get; set; } = false;        
        public float CastleRelocationCooldown { get; set; } = 60;        
        public bool InactivityKillEnabled { get; set; } = true;
        public int InactivityKillTimeMin { get; set; } = 3600;
        public int InactivityKillTimeMax { get; set; } = 604800;
        public int InactivityKillSafeTimeAddition { get; set; } = 172800;
        public int InactivityKillTimerMaxItemLevel { get; set; } = 84;
        public int StartingProgressionLevel { get; set; } = 0;
        public bool DisableDisconnectedDeadEnabled { get; set; } = true;
        public int DisableDisconnectedDeadTimer { get; set; } = 60;
        public double DisconnectedSunImmunityTime { get; set; } = 300.0;
        public double InventoryStacksModifier { get; set; } = 1.0;
        public double DropTableModifier_General { get; set; } = 1.0;
        public double DropTableModifier_Missions { get; set; } = 1.0;
        public double DropTableModifier_StygianShards { get; set; } = 1.0;
        public double MaterialYieldModifier_Global { get; set; } = 1.0;
        public double BloodEssenceYieldModifier { get; set; } = 1.0;
        public double JournalVBloodSourceUnitMaxDistance { get; set; } = 25.0;
        public double PvPVampireRespawnModifier { get; set; } = 1.0;
        public int CastleMinimumDistanceInFloors { get; set; } = 2;
        public int ClanSize { get; set; } = 4;
        public double BloodDrainModifier { get; set; } = 1.0;
        public double DurabilityDrainModifier { get; set; } = 1.0;
        public double GarlicAreaStrengthModifier { get; set; } = 1.0;
        public double HolyAreaStrengthModifier { get; set; } = 1.0;
        public double SilverStrengthModifier { get; set; } = 1.0;
        public double SunDamageModifier { get; set; } = 1.0;
        public double SoulShard_DurabilityLossRate { get; set; } = 1.0;
        public double CastleDecayRateModifier { get; set; } = 1.0;
        public double CastleBloodEssenceDrainModifier { get; set; } = 1.0;
        public double CastleSiegeTimer { get; set; } = 420.0;
        public double CastleUnderAttackTimer { get; set; } = 60.0;
        public double CastleRaidTimer { get; set; } = 600.0;
        public double CastleRaidProtectionTime { get; set; } = 1800.0;
        public double CastleExposedFreeClaimTimer { get; set; } = 300.0;
        public bool AnnounceSiegeWeaponSpawn { get; set; } = true;
        public bool ShowSiegeWeaponMapIcon { get; set; } = true;
        public double BuildCostModifier { get; set; } = 1.0;
        public double RecipeCostModifier { get; set; } = 1.0;
        public double CraftRateModifier { get; set; } = 1.0;
        public double ResearchCostModifier { get; set; } = 1.0;
        public double RefinementCostModifier { get; set; } = 1.0;
        public double RefinementRateModifier { get; set; } = 1.0;
        public double ResearchTimeModifier { get; set; } = 1.0;
        public double DismantleResourceModifier { get; set; } = 0.75;
        public double ServantConvertRateModifier { get; set; } = 1.0;
        public double RepairCostModifier { get; set; } = 1.0;
        public double Death_DurabilityFactorLoss { get; set; } = 0;
        public double Death_DurabilityLossFactorAsResources { get; set; } = 1.0;
        public int StarterEquipmentId { get; set; } = 0;
        public int StarterResourcesId { get; set; } = 0;
        public List<VBloodUnitSetting> VBloodUnitSettings { get; set; } = new List<VBloodUnitSetting>();
        public List<int> UnlockedAchievements { get; set; } = new List<int>();
        public List<int> UnlockedResearchs { get; set; } = new List<int>();
        public GameTimeModifiers GameTimeModifiers { get; set; } = new GameTimeModifiers();
        public VampireStatModifiers VampireStatModifiers { get; set; } = new VampireStatModifiers();
        public UnitStatModifiersGlobal UnitStatModifiers_Global { get; set; } = new UnitStatModifiersGlobal();
        public UnitStatModifiersVBlood UnitStatModifiers_VBlood { get; set; } = new UnitStatModifiersVBlood();
        public EquipmentStatModifiersGlobal EquipmentStatModifiers_Global { get; set; } = new EquipmentStatModifiersGlobal();
        public CastleStatModifiersGlobal CastleStatModifiers_Global { get; set; } = new CastleStatModifiersGlobal();
        public PlayerInteractionSettings PlayerInteractionSettings { get; set; } = new PlayerInteractionSettings();
        public TraderModifiers TraderModifiers { get; set; } = new TraderModifiers();
        public WarEventGameSettings WarEventGameSettings { get; set;} = new WarEventGameSettings();
    }

    public class ServerSettings
    {
        public string Name { get; set; } = "My V Rising Server";
        public string Description { get; set; } = "";
        public int Port { get; set; } = 9876;
        public int QueryPort { get; set; } = 9877;
        public string Address { get; set; } = "";
        public int MaxConnectedUsers { get; set; } = 40;
        public int MaxConnectedAdmins { get; set; } = 4;
        public int ServerFps { get; set; } = 30;
        public string SaveName { get; set; } = "world1";
        public string Password { get; set; } = "";
        public bool Secure { get; set; } = true;
        public bool ListOnSteam { get; set; } = false;
        public bool ListOnEOS { get; set; } = false;
        public int AutoSaveCount { get; set; } = 10;
        public int AutoSaveInterval { get; set; } = 120;
        public bool CompressSaveFiles { get; set; } = true;
        public int ResetDaysInterval { get; set; } = 0;
        public int DayOfReset { get; set; } = -1;
        public string GameSettingsPreset { get; set; } = "";
        public bool AdminOnlyDebugEvents { get; set; } = true;
        public bool DisableDebugEvents { get; set; } = false;
        public bool HideIPAddress { get; set; } = false;
        public API API { get; set; } = new API();
        public Rcon Rcon { get; set; } = new Rcon();
    }

    public class VoiceServicesSettings
    {
        public bool VOIPEnabled { get; set; } = false;
        public string VOIPIssuer { get; set; } = "";
        public string VOIPSecret { get; set; } = "";
        public string VOIPAppUserId { get; set; } = "";
        public string VOIPAppUserPwd { get; set; } = "";
        public string VOIPVivoxDomain { get; set; } = "";
        public string VOIPAPIEndpoint { get; set; } = "";
        public int VOIPConversationalDistance { get; set; } = 14;
        public int VOIPAudibleDistance { get; set; } = 40;
        public double VOIPConversationalSpeed { get; set; } = 2.0;
    }
    public class Achievement
    {
        [JsonIgnore]
        public string? Name { get; set; }
        public int Count { get; set; }
        public int ID { get; set; }
        [JsonIgnore]
        public bool Unlocked { get; set; }
    }

    public class Research
    {
        [JsonIgnore]
        public string? Name { get; set; }
        public int ID { get; set; }
        [JsonIgnore]
        public bool Unlocked { get; set; }
    }
}