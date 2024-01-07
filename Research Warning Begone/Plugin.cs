namespace Research_Warning_Begone;

using BepInEx;
using BepInEx.Logging;
using Game.UI;
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

    [HarmonyPatch(typeof(MainHud), nameof(MainHud.UpdateUnActivityResearchMessage)),HarmonyPrefix]
    public static bool UpdateUnActivityResearchMessage_Prefix()
    {
        return false;
    }
}
