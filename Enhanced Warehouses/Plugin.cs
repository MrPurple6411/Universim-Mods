namespace Enhanced_Warehouses;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using Game.Actors.Urban.Buildings;
using Game.Configs;
using Game.Planet;
using Game.UI;
using HarmonyLib;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<int> CapacityMultiplier { get; private set; }

    internal static ConfigEntry<bool> DefaultAllowNothing { get; private set; }

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Plugin settings
        CapacityMultiplier = Config.Bind("General", "Capacity Multiplier", 1, "Multiplier for the capacity of warehouses.");

        if (CapacityMultiplier.Value < 1)
            CapacityMultiplier.Value = 1;

        CapacityMultiplier.SettingChanged += (sender, args) =>
        {
            if (CapacityMultiplier.Value < 1)
                CapacityMultiplier.Value = 1;

            if (WarehouseConfig != null)
            {
                for (int i = 0; i < WarehouseStatsSheets.Length; i++)
                {
                    WarehouseStatsSheets[i].Capacity = DefaultCapacities[i] * CapacityMultiplier.Value;
                }

                SettlementController settlementController = MonoSingleton<PlanetInfo>.Instance.PlanetActor.SettlementController;
                settlementController.GetBuildingsOfType(BuildingConfig.Data.Type.Warehouse)?
                .Do(x => { if (x is WarehouseActor warehouseActor && warehouseActor.IsWarehouse()) warehouseActor.UpdateCapacity(); });
            }
        };

        DefaultAllowNothing = Config.Bind("General", "Default Allow Nothing", false, "If true, warehouses will allow nothing by default.");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} is loaded!");
    }

    internal static BuildingConfig WarehouseConfig { get; private set; }

    internal static WarehouseStatsSheet[] WarehouseStatsSheets { get; private set; }
    internal static int[] DefaultCapacities { get; private set; }

    // Patch to get defaults and set capacities
    [HarmonyPatch(typeof(ConfigInfo), nameof(ConfigInfo.BuildDictionaries)), HarmonyPrefix]
    public static void ConfigInfo_BuildDictionaries_Prefix(ConfigInfo __instance)
    {
        if (WarehouseConfig != null)
            return;

        foreach (var buildingConfig in __instance.ConfigInfoProperties.BuildingConfigs)
        {
            if (buildingConfig.Type != BuildingConfig.Data.Type.Warehouse)
                continue;

            WarehouseConfig = buildingConfig;

            WarehouseStatsSheets = new WarehouseStatsSheet[buildingConfig.StatsSheetsPerLvl.Count];
            DefaultCapacities = new int[buildingConfig.StatsSheetsPerLvl.Count];
            for (int i = 0; i < buildingConfig.StatsSheetsPerLvl.Count; i++)
            {
                WarehouseStatsSheet warehouseStatsSheet = buildingConfig.StatsSheetsPerLvl[i] as WarehouseStatsSheet;
                WarehouseStatsSheets[i] = warehouseStatsSheet;
                int capacity = warehouseStatsSheet.Capacity;
                if (DefaultCapacities[i] == default)
                    DefaultCapacities[i] = capacity;

                warehouseStatsSheet.Capacity = DefaultCapacities[i] * CapacityMultiplier.Value;
            }
            break;
        }
    }

    [HarmonyPatch(typeof(WarehouseActor), nameof(WarehouseActor.Setup)), HarmonyPostfix]
    public static void WarehouseActor_Setup_Postfix(WarehouseActor __instance, bool __state)
    {
        if (!__instance.IsWarehouse())
            return;

        if (__instance.UpgradeCache != null)
        {
            __instance.AcceptedToStoreResourceTypes = new System.Collections.Generic.HashSet<ResourceConfig.ResourceType>((__instance.UpgradeCache as WarehouseUpgradeCache).AcceptedToStoreResourceTypes);
            return;
        }

        if (!DefaultAllowNothing.Value)
            return;

        __instance.AcceptedToStoreResourceTypes.Clear();
    }

    [HarmonyPatch(typeof(WarehousePanel), nameof(WarehousePanel.Tick_Update)), HarmonyPostfix]
    public static void WarehousePanel_Tick_Update_Postfix(WarehousePanel __instance)
    {
        __instance.ToggleMassActionStorageBtn(__instance._owner.AcceptedToStoreResourceTypes.Count != 0);
    }

}