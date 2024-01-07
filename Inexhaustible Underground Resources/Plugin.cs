namespace Inexhaustible_Underground_Resources;

using BepInEx;
using BepInEx.Logging;
using Game;
using Game.Actors.Urban.Buildings;
using HarmonyLib;

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

    [HarmonyPatch(typeof(MineActor), nameof(MineActor.SubtractResource)), HarmonyPrefix]
    public static bool SubtractResource_Prefix(MineActor __instance, ref int __result)
    {
        if (__instance._depositLayer == null || __instance._depositsIndexes == null)
        {
            return true;
        }

        __result = 0;
        return false;
    }
}
