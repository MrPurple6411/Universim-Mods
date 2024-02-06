namespace GiantHugePlanets;

using BepInEx;
using BepInEx.Logging;
using Game.Actors.Planet;
using Game.Actors.Stats;
using Game.Actors.Urban.Buildings;
using Game.Configs;
using Game.Displays.Planets;
using Game.Scene;
using Game.Services;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    private void Awake()
    {
        Logger = base.Logger;

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(ChunkCuller), nameof(ChunkCuller.SetPlanet)), HarmonyPrefix]
    public static void SetPlanet_Prefix(ChunkCuller __instance, PlanetDisplay planetDisplay)
    {
        ChunkCullingPlanetConfig cullingPlanetConfig = null;
        int size = -1;
        foreach (var config in ChunkCuller.Instance.Config.PlanetConfigs)
        {
            if (config.PlanetSize == planetDisplay.Owner.PlanetSize)
            {
                return;
            }
            else if (config.PlanetSize > size)
            {
                size = config.PlanetSize;
                cullingPlanetConfig = config;
            }
        }

        if (cullingPlanetConfig != null && cullingPlanetConfig.PlanetSize != planetDisplay.Owner.PlanetSize)
        {
            var x = ScriptableObject.CreateInstance<ChunkCullingPlanetConfig>();

            foreach (var field in typeof(ChunkCullingPlanetConfig).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.Name == "PlanetSize") continue;

                field.SetValue(x, field.GetValue(cullingPlanetConfig));
            }

            x.PlanetSize = planetDisplay.Owner.PlanetSize;
            __instance.Config.PlanetConfigs = __instance.Config.PlanetConfigs.Append(x).ToArray();
        }
    }

    [HarmonyPatch(typeof(PlanetActor), nameof(PlanetActor.Setup)), HarmonyPrefix]
    public static void GetAvailableManagers_Prefix(PlanetActor __instance)
    {
        if (SceneLoader.Instance.overrideSize == 6)
            SceneLoader.Instance.overrideSize = 10;
    }
}
