namespace Enhanced_Observatory;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ForeignPlanetSimulation;
using Game;
using Game.Actors.Planet;
using Game.Actors.Urban.Buildings;
using Game.Configs;
using Game.Properties;
using Game.Starmap;
using Game.TUSpace;
using HarmonyLib;
using System.Collections;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal StarMapProperties StarMapProperties { get; private set; }

    // Default values
    private float DefaultRevealRadius;
    private float DefaultObservatoryExploreRange;

    // RevealRadiusMultiplier
    private static ConfigEntry<float> RevealRadiusMultiplier { get; set; }

    // ObservatoryExploreRangeMultiplier
    private static ConfigEntry<float> ObservatoryExploreRangeMultiplier { get; set; }

    // Instant Exploration
    private static ConfigEntry<bool> InstantExploration { get; set; }

    // Automatic Exploration
    private static ConfigEntry<bool> AutomaticExploration { get; set; }

    // Instant Recharge
    private static ConfigEntry<bool> InstantRecharge { get; set; }

    public void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        // Plugin settings
        RevealRadiusMultiplier = Config.Bind("General", "Reveal Radius Multiplier", 1.0f, "Multiplier for the Observatory's reveal radius.");
        RevealRadiusMultiplier.SettingChanged += (sender, args) =>
        {
            if (!StarMapProertiesExist())
                return;
            StarMapProperties.RevealRadius = DefaultRevealRadius * RevealRadiusMultiplier.Value;
        };

        ObservatoryExploreRangeMultiplier = Config.Bind("General", "Observatory Explore Range Multiplier", 1.0f, "Multiplier for the Observatory's explore range.");
        ObservatoryExploreRangeMultiplier.SettingChanged += (sender, args) =>
        {
            if (!StarMapProertiesExist())
                return;
            StarMapProperties.ObservatoryExploreRange = DefaultObservatoryExploreRange * ObservatoryExploreRangeMultiplier.Value;
        };

        InstantExploration = Config.Bind("General", "Instant Exploration", false, "Instantly explore objects selected to be explored by the Observatory.");
        AutomaticExploration = Config.Bind("General", "Automatic Exploration", false, "Automatically explore objects visable to the Observatory when fully charged.");
        InstantRecharge = Config.Bind("General", "Instant Recharge", false, "Instantly recharge the Observatory's energy.");

        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} is loaded!");
    }

    public IEnumerator Start()
    {
        yield return new WaitUntil(StarMapProertiesExist);
        Logger.LogInfo("StarMapProperties exist!");
    }

    private bool StarMapProertiesExist()
    {
        try
        {
            StarMapProperties = PlanetActor.StarMapData();
            if (StarMapProperties != null)
            {
                if (DefaultRevealRadius == default)
                    DefaultRevealRadius = StarMapProperties.RevealRadius;
                if (DefaultObservatoryExploreRange == default)
                    DefaultObservatoryExploreRange = StarMapProperties.ObservatoryExploreRange;

                StarMapProperties.RevealRadius = DefaultRevealRadius * RevealRadiusMultiplier.Value;
                StarMapProperties.ObservatoryExploreRange = DefaultObservatoryExploreRange * ObservatoryExploreRangeMultiplier.Value;
                return true;
            }

            return StarMapProperties != null;
        }
        catch
        {
            return false;
        }
    }

    // prefix SpaceObject.Explore to set ExplorationProgress to 1
    [HarmonyPatch(typeof(SpaceObject), nameof(SpaceObject.Explore)), HarmonyPrefix]
    public static void Explore_Prefix(SpaceObject __instance)
    {
        if (InstantExploration.Value)
            __instance.ExplorationProgress = 1;
    }

    // postfix ObservatoryActor.ObjectBeingExplored to auto assign the object to be explored
    [HarmonyPatch(typeof(ObservatoryActor), nameof(ObservatoryActor.ObjectBeingExplored)), HarmonyPostfix]
    public static void ObjectBeingExplored_Postfix(ObservatoryActor __instance, ref SpaceObject __result)
    {
        if (__result != null ||!AutomaticExploration.Value || __instance.Charge < __instance.ObservatoryStatSheet.MaxCharge)
            return;

        foreach (SpaceObject spaceObject in __instance.SpaceControl().SpaceObjects)
        {
            if (!spaceObject.BeingExplored && spaceObject.Revealed)
            {
                spaceObject.StartExplorationFrom(__instance.SpaceCoordinates());
                __instance.AssignedSpaceObject = spaceObject;
                __result = __instance.AssignedSpaceObject;
                return;
            }
        }
    }

    // postfix ObservatoryActor.OnTick to instantly recharge the Observatory and make sure it doesnt over charge.
    [HarmonyPatch(typeof(ObservatoryActor), nameof(ObservatoryActor.OnTick)), HarmonyPostfix]
    public static void OnTick_Postfix(ObservatoryActor __instance)
    {
        if (InstantRecharge.Value)
        {
            __instance._updateTimer = 0;
            __instance.Planet._observatoryCharge = __instance.ObservatoryStatSheet.MaxCharge;
        }
    }

    // postfix ForeignBuilding.DoWork to instantly recharge the Observatory and make sure it doesnt over charge.
    [HarmonyPatch(typeof(ForeignBuilding), nameof(ForeignBuilding.DoWork)), HarmonyPostfix]
    public static void DoWork_Postfix(ForeignBuilding __instance)
    {
        if (InstantRecharge.Value && __instance.stats is ObservatoryStatsSheet observatoryStatsSheet)
        {
            __instance.planet._observatoryCharge = observatoryStatsSheet.MaxCharge;
        }
    }

    // prefix PlanetActor.ObservatoryCharge property setter to reject a lower value if InstantRecharge is enabled.
    [HarmonyPatch(typeof(PlanetActor), nameof(PlanetActor.ObservatoryCharge), MethodType.Setter), HarmonyPrefix]
    public static bool ObservatoryCharge_Setter_Prefix(PlanetActor __instance, float value)
    {
        return !InstantRecharge.Value || __instance._observatoryCharge < value;
    }
    // prefix ForeignPlanet.ObservatoryCharge property setter to reject a lower value if InstantRecharge is enabled.
    [HarmonyPatch(typeof(ForeignPlanet), nameof(ForeignPlanet.ObservatoryCharge), MethodType.Setter), HarmonyPrefix]
    public static bool ObservatoryCharge_Setter_Prefix(ForeignPlanet __instance, float value)
    {
        return !InstantRecharge.Value || __instance._observatoryCharge < value;
    }
}