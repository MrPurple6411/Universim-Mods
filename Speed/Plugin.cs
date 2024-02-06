namespace Speed;

using BepInEx;
using BepInEx.Logging;
using Game;
using Game.Actors;
using Game.Actors.NaturalResources;
using Game.Actors.Pawns;
using Game.Actors.Stats;
using Game.Actors.Urban.Buildings;
using Game.Configs;
using Game.Research;
using Game.Services;
using Game.UI;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(NuggetSpeedStat), MethodType.Constructor, new System.Type[] { typeof(StageActor), typeof(int) }), HarmonyPostfix]
    public static void Constructor_Prefix(NuggetSpeedStat __instance, StageActor statOwner, int statType)
    {
        if (statOwner is not CitizenActor citizenActor || citizenActor.IsExiled)
            return;

        __instance.SetBase(__instance.Max);
        __instance.SetValue(__instance.Base);
    }

    [HarmonyPatch(typeof(ConstructionSiteActor), nameof(ConstructionSiteActor.Construct)), HarmonyPrefix]
    public static void Construct_Prefix(ConstructionSiteActor __instance, ref float amount)
    {
        amount = __instance.MaxConstructionHealth;
    }

    [HarmonyPatch(typeof(TreeActor), nameof(TreeActor.TakeHealth)), HarmonyPrefix]
    public static void TakeHealth_Prefix(TreeActor __instance, ref float damage, DamageSourceTypes damageSourceType, bool fromGodPower)
    {
        if (__instance.Health.Value <= 0f)
        {
            return;
        }

        if (__instance.IsPicked)
        {
            return;
        }

        if (fromGodPower || damageSourceType != DamageSourceTypes.Other)
            return;

        damage = __instance.Health.Value;
    }

    [HarmonyPatch(typeof(ResourceActor), nameof(ResourceActor.TakeHealth)), HarmonyPrefix]
    public static void TakeHealth_Prefix(ResourceActor __instance, ref float damage, DamageSourceTypes damageSourceType, bool fromGodPower)
    {
        if (__instance.Health.Value <= 0f)
        {
            return;
        }

        if (__instance.IsPicked)
        {
            return;
        }

        if (fromGodPower || damageSourceType != DamageSourceTypes.Other)
            return;

        damage = __instance.Health.Value;
    }

    [HarmonyPatch(typeof(BuildingIcon), nameof(BuildingIcon.Initialize)), HarmonyPrefix]
    public static void Initialize_Prefix(BuildingIcon __instance, ref BuildingConfig buildingConfig)
    {
        if (buildingConfig.BuildingLimit > 1)
            buildingConfig.BuildingLimit = 0;

        if (buildingConfig.RequiredBlueprint != null)
            buildingConfig.RequiredBlueprint = null;

        if (buildingConfig.RequiredQuest != null)
            buildingConfig.RequiredQuest = null;

        if (buildingConfig.RequiredResearch != null && buildingConfig.RequiredResearch.Count > 0)
            buildingConfig.RequiredResearch.Clear();
    }

    [HarmonyPatch(typeof(BuildingConfig), nameof(BuildingConfig.GetMaxStatsByEra)), HarmonyPrefix]
    public static bool GetMaxStatsByEra_Postfix(BuildingConfig __instance, EraType eraType, ref int statLevel, ref BuildingStatSheet __result)
    {
        for (int i = 0; i < __instance.StatsSheetsPerLvl.Count; i++)
        {
            BuildingStatSheet buildingStatSheet2 = __instance.StatsSheetsPerLvl[i];
            if (buildingStatSheet2.EraType <= (__result?.EraType ?? EraType.SPACE_AGE))
            {
                __result = buildingStatSheet2;
                statLevel = i;
            }
        }
        return false;
    }

    [HarmonyPatch(typeof(BuildingActor), nameof(BuildingActor.GetUpgradePerks)), HarmonyPrefix]
    public static bool GetUpgradePerks_Postfix(BuildingActor __instance, ref List<Perk> __result)
    {
        __result = null;
        return false;
    }

    // Instant Factories
    [HarmonyPatch(typeof(FactoryActor), nameof(FactoryActor.ProcessResources)), HarmonyPrefix]
    public static void ProcessResources_Prefix(FactoryActor __instance)
    {
        __instance._processTime = __instance._currentProductionTime;
    }

    // Instant Factories
    [HarmonyPatch(typeof(MineActor), nameof(MineActor.ProcessResources)), HarmonyPrefix]
    public static void ProcessResources_Prefix(MineActor __instance)
    {
        __instance._processTime = __instance._currentMiningTime;
    }

    [HarmonyPatch(typeof(BuildingActor), nameof(BuildingActor.OnTick)), HarmonyPrefix]
    public static void OnTick_Prefix(BuildingActor __instance)
    {
        if (__instance.GetBuildingType() != BuildingConfig.Data.Type.LumberMill)
            return;

        if (__instance is not LumberMillActor lumberMillActor)
        {
            Logger.LogError("LumberMillActor is null!");
            return;
        }

        foreach (var tree in lumberMillActor.growingPlantedTrees)
        {
            if (tree == null || Mathf.Approximately(tree._growingTimeRemaining, 0f))
                continue;

            tree._growingTimeRemaining = 0f;
        }
    }

    // instant crop growth
    [HarmonyPatch(typeof(FarmActor), nameof(FarmActor.GetTimeToGrow)), HarmonyPrefix]
    public static bool GetTimeToGrow_Prefix(FarmActor __instance, ref int __result)
    {
        __result = __instance.CurrentCrop == null ? 0 : 1;
        return false;
    }

    // 100x yield from Farms
    [HarmonyPatch(typeof(FarmActor), nameof(FarmActor.DepositYield)), HarmonyPrefix]
    public static void DepositYield_Prefix(FarmActor __instance, FarmSpot spot)
    {
        if (spot.CropActor.CurrentCropQuantity > 0)
            spot.CropActor.CurrentCropQuantity *= 100;
    }

    // instant drinking water from WellActor
    [HarmonyPatch(typeof(WellActor), nameof(WellActor.OnTick)), HarmonyPrefix]
    public static void OnTick_Prefix(WellActor __instance)
    {
        __instance._refillTimer = __instance._statsSheet.SecondsPerOneDrinkingWater;
    }    
}
