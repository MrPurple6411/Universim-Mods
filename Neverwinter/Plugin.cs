namespace Neverwinter;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Faith;
using Game.Planet;
using HarmonyLib;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    private static ConfigEntry<bool> SkipCostsCP;

    private void Awake()
    {
        Logger = base.Logger;

        SkipCostsCP = Config.Bind(MyPluginInfo.PLUGIN_NAME, "Skip Costs CP", false, "Does the auto skip cost the normal creator points charge?");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(ClimateManager), nameof(ClimateManager.Initialize)), HarmonyPostfix]
    public static void ClimateManager_Initialize_Postfix(ClimateManager __instance)
    {
        __instance.Seasons.OnSeasonChanged += (cs) =>
        {
            if (!cs.isChangingSeasonByGodPower && cs.CurrentSeason.SeasonIdx == 3)
                WinterStarted(__instance);
        };
    }

    private static void WinterStarted(ClimateManager climateManager)
    {
        var planet = climateManager._planet;
        var faithController = planet.FaithController;
        var currentPower = faithController.CurrentGodPowerConfig;

        faithController.SetCurrentGodPower(Game.Configs.GodPowerConfig.Data.Type.SeasonChange);
        var seasonPowerController = faithController.GodPowerWrapper as GodPower_SeasonChange;
        faithController.SetCurrentGodPower(currentPower);

        if (seasonPowerController == null)
        {
            Logger.LogError("seasonPowerController is null");
            return;
        }

        if (!SkipCostsCP.Value)
        {
            seasonPowerController.ChangeSeason(0);
            return;
        }

        int seasonChangeCost = climateManager.GetSeasonChangeCost(0);
        if (faithController.ConsumePowerPoints(seasonChangeCost))
        {
            seasonPowerController.ChangeSeason(0);
        }
    }
}
