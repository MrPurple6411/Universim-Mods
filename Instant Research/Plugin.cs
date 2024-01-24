namespace Instant_Research;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Research;
using Game.Sound;
using Game.UI;
using HarmonyLib;
using System.Collections;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    internal static ConfigEntry<bool> _instantResearch;

    private void Awake()
    {
        Logger = base.Logger;

        _instantResearch = Config.Bind("General", "InstantResearch", true, "Instantly complete researches");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private IEnumerator Start()
    {
        while (true)
        {


            yield return new WaitForSecondsRealtime(1f);
        }
    }

    [HarmonyPatch(typeof(ResearchController), nameof(ResearchController.StartResearch))]
    [HarmonyPostfix]
    public static void GetAvailableManagers_Postfix(ResearchController __instance, Perk perk)
    {
        if (clicked || !_instantResearch.Value || __instance.CurrentResearch == null)
        {
            if (!_instantResearch.Value)
                Logger.LogInfo("Instant research disabled");
            return;
        }

        __instance.CompleteCurrentResearch();
    }

    private static bool clicked = false;

    [HarmonyPatch(typeof(ResearchScreen), nameof(ResearchScreen.AddToQueue)), HarmonyPrefix]
    public static bool CompleteCurrentResearch_Postfix(ResearchScreen __instance, ResearchScreenPerkItem perkUIItem)
    {
        if (!_instantResearch.Value || __instance._researchController.PerkQueue.Count >= __instance.queueSlots.Length)
        {
            Logger.LogInfo("Instant research disabled");
            return true;
        }
        clicked = true;
        __instance._researchController.AddPerkToQueue(perkUIItem.perk);
        perkUIItem.UpdateSetPerks();
        __instance.UpdateQueueStates();
        __instance.queueSlots[__instance._researchController.PerkQueue.Count - 1].PlayAddToQueueAnimation();
        MonoSingleton<SoundManager>.Instance.PlayAndForget2D(SoundType.RESEARCH_STARTED_UI, 1f);
        clicked = false;

        __instance._researchController.CompleteCurrentResearch();
        return false;
    }
}
