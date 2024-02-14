namespace Inexhaustible_Underground_Resources;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using HarmonyLib;
using System.Diagnostics;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<bool> _inexhaustibleResources { get; private set; }
    internal static ConfigEntry<bool> _abundantResources { get; private set; }

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        _inexhaustibleResources = Config.Bind("General", "Inexhaustible Resources", true, "Inexhaustible resources (Makes it so resources never run out)");
        _abundantResources = Config.Bind("General", "Abundant Resources", false, "Abundant resources (Makes it so planets have HUGE quantities of resources)");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
    }

    // Prefix to make resources abundant
    [HarmonyPatch(typeof(UndergroundResourceLayer), nameof(UndergroundResourceLayer.InitResources)), HarmonyPostfix]
    public static void InitResources_Postfix(UndergroundResourceLayer __instance)
    {
        if (!_abundantResources.Value)
            return;

        for (int i = 0; i < __instance.resources.Length; i++)
        {
            __instance.FillResource(i);
        }
    }

    // Prefix to make resources inexhaustible
    [HarmonyPatch(typeof(UndergroundResourceLayer), nameof(UndergroundResourceLayer.SubtractResource)), HarmonyPrefix]
    public static bool SubtractResource_Prefix(UndergroundResourceLayer __instance, int nodeIndex, int remaining, ref int __result)
    {
        // Log what methods called this method
        if (new StackTrace().ToString().Contains("Cracker") || !_inexhaustibleResources.Value)
            return true;

        __result = remaining - Mathf.Min(remaining, __instance.resources[nodeIndex]);
        return false;
    }
}
