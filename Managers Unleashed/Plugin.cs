using System.Linq;

namespace Managers_Unleashed
{
    using BepInEx;
    using BepInEx.Configuration;
    using BepInEx.Logging;
    using Game;
    using Game.Actors;
    using Game.Actors.Pawns;
    using Game.Actors.Stats;
    using Game.Actors.Urban.Buildings;
    using Game.Configs;
    using Game.Planet;
    using Game.UI;
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using Utils.General;

    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger { get; private set; }
        internal static ConfigEntry<bool> NeedMinistersAssigned { get; private set; }
        internal static ConfigEntry<bool> AutoUpgradeBuildings { get; private set; }
        internal static ConfigEntry<bool> AutoAssignGovenor { get; private set; }

        // auto assign ministers configs
        internal static readonly Dictionary<BuildingStatType, ConfigEntry<bool>> AutoAssignMinisters = new Dictionary<BuildingStatType, ConfigEntry<bool>>();

        private void Awake()
        {
            if (!Analytics.AnalyticsDisabled)
                Analytics.DisableAnalytics();

            Logger = base.Logger;

            NeedMinistersAssigned = Config.Bind("General", "Need Ministers Assigned", true, "Need ministers assigned for buildings to be built.");
            AutoUpgradeBuildings = Config.Bind("General", "Auto Upgrade Buildings", false, "Automatically upgrade buildings when possible.");
            AutoAssignGovenor = Config.Bind("Auto Assign Nugget", "Governor", true, "Automatically assign governor to the settlement if noone is assigned.");
            
            foreach (BuildingStatType ministerSlot in from statType in Enum.GetValues(typeof(BuildingStatType)) as BuildingStatType[] where statType.ToString().Contains("MinisterOf") select statType)
            {
                string MinisterName = $"MinisterJob/{ministerSlot.ToString().ToUpper()}".TranslateText();
                AutoAssignMinisters.Add(ministerSlot, Config.Bind("Auto Assign Nugget", $"{MinisterName}", false, $"Automatically assign {MinisterName} minister to the settlement if noone is assigned."));
            }

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
            if (NeedMinistersAssigned.Value)
            {
                if (__instance.Planet.SettlementController.AssignedNuggetPercentage >= __instance.AutoAssignPercentage || __instance.AllMinisterSlotsFilled())
                {
                    return true;
                }

                foreach (var ministerSlot in AutoAssignMinisters)
                {
                    int index = __instance.ministersSlotsTypes.IndexOf(ministerSlot.Key);

                    if (index == -1 || !ministerSlot.Value.Value)
                        continue;

                    if (__instance._ministersSlots[index] != null)
                        continue;

                    NuggetActor nuggetAvailableForAutoAssign = __instance.Planet.SettlementController.GetNuggetAvailableForAutoAssign(__instance, null);
                    if (nuggetAvailableForAutoAssign != null && __instance.OnAssignMinisterNuggetChoosed(nuggetAvailableForAutoAssign, ministerSlot.Key))
                    {
                        Logger.LogInfo($"Auto assigned {nuggetAvailableForAutoAssign.Name} as {$"MinisterJob/{ministerSlot.Key.ToString().ToUpper()}".TranslateText()}");
                    }
                }

                return true;
            }

            __instance.ministersSlotsTypes.ForEach(x => __instance.ministersSystem.UpdateTimer(x, timeDelta));
            return false;
        }

        [HarmonyPatch(typeof(EmploymentCenterActor), nameof(EmploymentCenterActor.OnTick)), HarmonyPostfix]
        public static void OnTick_Postfix(EmploymentCenterActor __instance)
        {
            if (__instance.Nuggets == null || __instance.Nuggets.Count > 0 || !AutoAssignGovenor.Value)
                return;

            if (!__instance._isWorking)
                __instance.Planet.SettlementController.AutoAssignNugget(__instance, null);
        }

        [HarmonyPatch(typeof(BuildingActor), nameof(BuildingActor.OnTick)), HarmonyPostfix]
        public static void OnTick_Postfix(BuildingActor __instance)
        {
            if (!AutoUpgradeBuildings.Value || !__instance.IsUpgradeReady)
                return;

            __instance.UpgradeBuilding();
            if (__instance.Planet.GameTipsController.OnDoUpgradeButtonPressed != null)
            {
                __instance.Planet.GameTipsController.OnDoUpgradeButtonPressed(__instance);
            }
        }

        [HarmonyPatch(typeof(Messages), nameof(Messages.MinisterBuildingMessage)), HarmonyPrefix]
        public static bool MinisterBuildingMessage_Prefix(Messages __instance, ref BuildingStatType ministerSlot, ConstructionSiteActor building, NewsConfig.Data.Trigger trigger)
        {
            if (NeedMinistersAssigned.Value)
                return true;

            Dictionary<string, GameObject> dictionary = new Dictionary<string, GameObject>();
            Dictionary<string, string> dictionary2 = new Dictionary<string, string>();
            StringBuilder stringBuilder = new StringBuilder("MinisterJob/");
            stringBuilder.Append(ministerSlot.ToString().ToUpper());
            EmploymentCenterActor employmentCenterActor = MonoSingleton<PlanetInfo>.Instance.PlanetActor.SettlementController.GetBuildingsOfType(BuildingConfig.Data.Type.EmploymentCenter)[0] as EmploymentCenterActor;
            string text = stringBuilder.ToString().TranslateText();
            dictionary2.Add("#Minister_Slot_Name", text);
            dictionary.Add(text, employmentCenterActor.GetNuggetAt(0).Display.Transform.gameObject);
            string text2 = building.BuildingConfig.GetCurrentLevelStats(-1).TranslationKey_Name.TranslateText();
            dictionary2.Add("#Building_Name", text2);
            dictionary.Add(text2, building.Display.Transform.gameObject);
            NewsController.Instance.NewMessage(trigger, null, dictionary2, dictionary, null, DamageSourceTypes.Unknown);

            return false;
        }
    }
}
