namespace Managers_Unleashed
{
    using BepInEx;
    using BepInEx.Configuration;
    using BepInEx.Logging;
    using Game.Actors.Stats;
    using Game.Actors.Urban.Buildings;
    using HarmonyLib;

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger { get; private set; }
        internal static ConfigEntry<bool> needMinistersAssigned;
        internal static ConfigEntry<bool> autoUpgradeBuildings;

        private void Awake()
        {
            Logger = base.Logger;

            needMinistersAssigned = Config.Bind("General", "NeedMinistersAssigned", true, "Need ministers assigned for buildings to be built.");
            autoUpgradeBuildings = Config.Bind("General", "AutoUpgradeBuildings", false, "Automatically upgrade buildings when possible.");

            // Harmony patching
            Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

            // Plugin startup logic
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        [HarmonyPatch(typeof(EmploymentCenterActor), nameof(EmploymentCenterActor.TryUnlockSlot))]
        [HarmonyPostfix]
        public static void GetAvailableManagers_Postfix(EmploymentCenterActor __instance, BuildingStatType statType)
        {
            if (!__instance.ministersSlotsTypes.Contains(statType) || __instance.unlockedSlots.Contains(statType))
            {
                return;
            }

            __instance.unlockedSlots.Add(statType);
            if (__instance.onSlotUnlocked != null)
            {
                __instance.onSlotUnlocked(statType);
            }
        }

        [HarmonyPatch(typeof(EmploymentCenterActor), nameof(EmploymentCenterActor.UpdateMinistersSystem)), HarmonyPrefix]
        public static bool UpdateMinistersSystem_Prefix(EmploymentCenterActor __instance, float timeDelta)
        {
            if (needMinistersAssigned.Value)
                return true;

            __instance.ministersSlotsTypes.ForEach(x => __instance.ministersSystem.UpdateTimer(x, timeDelta));
            return false;
        }

        [HarmonyPatch(typeof(BuildingActor), nameof(BuildingActor.OnTick)), HarmonyPostfix]
        public static void OnTick_Postfix(BuildingActor __instance)
        {
            if (!autoUpgradeBuildings.Value || !__instance.IsUpgradeReady)
                return;

            __instance.UpgradeBuilding();
            if (__instance.Planet.GameTipsController.OnDoUpgradeButtonPressed != null)
            {
                __instance.Planet.GameTipsController.OnDoUpgradeButtonPressed(__instance);
            }
        }

    }
}
