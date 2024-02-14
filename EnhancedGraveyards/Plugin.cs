namespace EnhancedGraveyards;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrytivoClubHelpers.StaticCoroutine;
using Game;
using Game.Actors.Pawns;
using Game.Actors.Stats;
using Game.Actors.Urban.Buildings;
using Game.Configs;
using Game.Planet;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<float> MaxWorkRangeMultiplier { get; private set; }
    internal static ConfigEntry<float> WorkerSpeedMultiplier { get; private set; }
    internal static ConfigEntry<bool> InstantlyMoveDead { get; private set; }

    internal static ConfigEntry<bool> InstantlyDestroyDead { get; private set; }

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Plugin settings
        MaxWorkRangeMultiplier = Config.Bind("General", "Max Work Range Multiplier", 1.0f, "Multiplier for the maximum work range of graveyards.");

        if (MaxWorkRangeMultiplier.Value < 1.0f)
        {
            MaxWorkRangeMultiplier.Value = 1.0f;
        }

        MaxWorkRangeMultiplier.SettingChanged += OnMaxWorkDistanceMultiplierChange;

        WorkerSpeedMultiplier = Config.Bind("General", "Worker Movement Speed Multiplier", 1.0f, "Multiplier for the speed of workers in graveyards.");

        if (WorkerSpeedMultiplier.Value < 1.0f)
        {
            WorkerSpeedMultiplier.Value = 1.0f;
        }

        WorkerSpeedMultiplier.SettingChanged += OnWorkerSpeedMultiplierChange;

        InstantlyMoveDead = Config.Bind("General", "Instantly Move Dead", false, "If true, dead nuggets will be instantly moved to a graveyard if in range.");
        InstantlyDestroyDead = Config.Bind("General", "Instantly Destroy Dead", false, "If true, dead nuggets will be instantly destroyed.");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} is loaded!");
    }

    // SettlementController.AddDepartedNuggetRequest
    [HarmonyPatch(typeof(SettlementController), nameof(SettlementController.AddDepartedNuggetRequest)), HarmonyPrefix]
    public static bool AddDepartedNuggetRequest_Prefix(SettlementController __instance, CitizenActor nugget)
    {
        if (InstantlyMoveDead.Value || InstantlyDestroyDead.Value)
        {
            StaticCoroutine.StartCoroutine(ProcessDeadNugget(nugget));
            return false;
        }

        return true;
    }

    // wait 3 seconds before processing the dead nugget in a coroutine
    private static IEnumerator ProcessDeadNugget(CitizenActor nugget)
    {
        while (true)
        {
            yield return new WaitForSeconds(3f);

            if (nugget == null)
                yield break;

            if (InstantlyDestroyDead.Value)
            {
                nugget.Destroy();
                yield break;
            }

            bool moved = false;

            SettlementController settlementController = MonoSingleton<PlanetInfo>.Instance.PlanetActor.SettlementController;
            settlementController.GetBuildingsOfType(BuildingConfig.Data.Type.Cemetery)?.Do(x =>
            {
                if (!moved && x is GraveyardActor cemeteryActor)
                {
                    if (!cemeteryActor.IsFinished || cemeteryActor.GetFreeSpot() == null)
                        return;

                    if (cemeteryActor.Planet.RoughDistanceOnSurface(cemeteryActor.GridCoord.worldPosition, nugget.WorldPosition) <= cemeteryActor._maxWorkingRange)
                    {
                        cemeteryActor.BurryNugget(nugget);
                        nugget.Destroy();
                        moved = true;
                    }
                }
            });

            if (moved)
                yield break;
        }
    }

    private void OnWorkerSpeedMultiplierChange(object sender, EventArgs e)
    {
        if (WorkerSpeedMultiplier.Value <= 1.0f)
        {
            WorkerSpeedMultiplier.Value = 1.0f;
        }

        if (CemeteryStatSheets == null)
            return;

        SettlementController settlementController = MonoSingleton<PlanetInfo>.Instance.PlanetActor.SettlementController;
        List<GraveyardActor> cemeteryActors = settlementController.GetBuildingsOfType(BuildingConfig.Data.Type.Cemetery).Where(x => x is GraveyardActor).Cast<GraveyardActor>().ToList();

        foreach (GraveyardActor cemeteryActor in cemeteryActors)
        {
            foreach (var worker in cemeteryActor.Nuggets)
            {
                worker.MovementSpeed.RemoveEffect(cemeteryActor.CemeteryStatsSheet.speedEffect);
            }
        }

        for (int i = 0; i < CemeteryStatSheets.Length; i++)
        {
            CemeteryStatSheets[i].speedEffect.Amount = DefaultSpeedEffect[i] * MaxWorkRangeMultiplier.Value;
        }

        foreach (GraveyardActor cemeteryActor in cemeteryActors)
        {
            cemeteryActor.CemeteryStatsSheet = cemeteryActor.GetStats<CemeteryStatsSheet>();
            foreach (var worker in cemeteryActor.Nuggets)
            {
                worker.MovementSpeed.AddEffect(new StatEffect(cemeteryActor.CemeteryStatsSheet.speedEffect, -1));
            }
        }
    }

    private void OnMaxWorkDistanceMultiplierChange(object sender, EventArgs e)
    {
        if (MaxWorkRangeMultiplier.Value <= 1.0f)
        {
            MaxWorkRangeMultiplier.Value = 1.0f;
        }

        if (CemeteryStatSheets == null)
            return;

        for (int i = 0; i < CemeteryStatSheets.Length; i++)
        {
            CemeteryStatSheets[i].MaxWorkingRange = DefaultMaxWorkRange[i] * MaxWorkRangeMultiplier.Value;
        }

        SettlementController settlementController = MonoSingleton<PlanetInfo>.Instance.PlanetActor.SettlementController;
        List<GraveyardActor> cemeteryActors = settlementController.GetBuildingsOfType(BuildingConfig.Data.Type.Cemetery).Where(x => x is GraveyardActor).Cast<GraveyardActor>().ToList();
        foreach (GraveyardActor cemeteryActor in cemeteryActors)
        {
            cemeteryActor.CemeteryStatsSheet = cemeteryActor.GetStats<CemeteryStatsSheet>();
            cemeteryActor._maxWorkingRange = cemeteryActor.CemeteryStatsSheet.MaxWorkingRange;
            cemeteryActor._workingRange = cemeteryActor._maxWorkingRange;
        }
    }

    internal static BuildingConfig CemeteryConfig { get; private set; }

    internal static CemeteryStatsSheet[] CemeteryStatSheets { get; private set; }
    internal static float[] DefaultMaxWorkRange { get; private set; }
    internal static float[] DefaultSpeedEffect { get; private set; }

    [HarmonyPatch(typeof(ConfigInfo), nameof(ConfigInfo.BuildDictionaries)), HarmonyPrefix]
    public static void ConfigInfo_BuildDictionaries_Prefix(ConfigInfo __instance)
    {
        if (CemeteryConfig != null)
            return;

        foreach (var buildingConfig in __instance.ConfigInfoProperties.BuildingConfigs)
        {
            if (buildingConfig.Type != BuildingConfig.Data.Type.Cemetery)
                continue;

            CemeteryConfig = buildingConfig;
            CemeteryStatSheets = new CemeteryStatsSheet[buildingConfig.StatsSheetsPerLvl.Count];
            DefaultMaxWorkRange = new float[buildingConfig.StatsSheetsPerLvl.Count];
            DefaultSpeedEffect = new float[buildingConfig.StatsSheetsPerLvl.Count];
            for (int i = 0; i < buildingConfig.StatsSheetsPerLvl.Count; i++)
            {
                CemeteryStatsSheet cemeteryStatSheet = buildingConfig.StatsSheetsPerLvl[i] as CemeteryStatsSheet;
                CemeteryStatSheets[i] = cemeteryStatSheet;
                DefaultMaxWorkRange[i] = cemeteryStatSheet.MaxWorkingRange;
                DefaultSpeedEffect[i] = cemeteryStatSheet.speedEffect.Amount;
                cemeteryStatSheet.MaxWorkingRange = cemeteryStatSheet.MaxWorkingRange * MaxWorkRangeMultiplier.Value;
                cemeteryStatSheet.speedEffect.Amount = cemeteryStatSheet.speedEffect.Amount * MaxWorkRangeMultiplier.Value;
            }
            break;
        }
    }
}