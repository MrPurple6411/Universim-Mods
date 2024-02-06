namespace Inexhaustible_Unobtanium;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using Game.Actors.Urban.Buildings;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    private const float DefaultMultiplier = 1.0f;

    // default cycle period of the unobtanium extractor at the time this mod was made.
    private const float _defaultCyclePeriod = 600f;

    public static ConfigEntry<float> CycleSpeedMultiplier { get; private set; } 

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Config
        CycleSpeedMultiplier = Config.Bind(MyPluginInfo.PLUGIN_NAME, "Cycle Speed Multiplier", DefaultMultiplier, "Multiplier for the cycle speed of the unobtanium extractor.");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(ArtifactExtractorActor), nameof(ArtifactExtractorActor.OnCycleComplete)), HarmonyPrefix]
    public static bool OnCycleComplete_Prefix(ArtifactExtractorActor __instance)
    {
        return __instance.GetBuildingType() != Game.Configs.BuildingConfig.Data.Type.UnobtaniumExtractor;
    }

    private static HashSet<ArtifactExtractorActor> Actors = new HashSet<ArtifactExtractorActor>();
    private static float lastMultiplier = DefaultMultiplier;

    [HarmonyPatch(typeof(ArtifactExtractorActor), nameof(ArtifactExtractorActor.UpdateProgress)), HarmonyPrefix]
    public static void UpdateProgress_Prefix(ArtifactExtractorActor __instance)
    {
        float currentMultiplier = CycleSpeedMultiplier.Value;
        if (currentMultiplier != lastMultiplier)
        {
            lastMultiplier = currentMultiplier;
            Actors.Clear();
        }

        if(__instance.GetBuildingType() != Game.Configs.BuildingConfig.Data.Type.UnobtaniumExtractor)
            return;

        if (Actors.Contains(__instance))
            return;

        var currentCyclePeriod = __instance._cyclePeriod;
        __instance._cyclePeriod = _defaultCyclePeriod / Mathf.Max(currentMultiplier, 0.001f);

        if (__instance._currentCycleTime > 0f)
        {
            var currentCycleTime = __instance._currentCycleTime;
            var currentPercentage = currentCycleTime / currentCyclePeriod;
            __instance._currentCycleTime = __instance._cyclePeriod * currentPercentage;
        }
        Actors.Add(__instance);
    }
}