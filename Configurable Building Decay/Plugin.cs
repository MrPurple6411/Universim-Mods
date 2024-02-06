namespace Configurable_Building_Decay;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using Game.Actors.Urban.Buildings;
using HarmonyLib;
using System.Collections.Generic;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }
    private static ConfigEntry<int> _decayRate;

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Plugin settings
        // Decay rate 0 = no decay 100 = default decay 200 = double decay etc
        _decayRate = Config.Bind(MyPluginInfo.PLUGIN_NAME, "DecayRate", 100, "Decay rate 0 = no decay 100 = default decay 200 = double decay etc");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static Dictionary<BuildingActor, float> DefaultDecayRates = new Dictionary<BuildingActor, float>();

    [HarmonyPatch(typeof(BuildingActor), nameof(BuildingActor.DamageBuildingInTime)), HarmonyPrefix]
    private static void DamageBuildingInTimePrefix(BuildingActor __instance)
    {
        if (!DefaultDecayRates.TryGetValue(__instance, out float decayRate))
        {
            decayRate = DefaultDecayRates[__instance] = __instance.DamagePerSecond;
        }

        var decayRateMultiplier = _decayRate.Value / 100f;
        __instance.DamagePerSecond = decayRate * decayRateMultiplier;
    }
}
