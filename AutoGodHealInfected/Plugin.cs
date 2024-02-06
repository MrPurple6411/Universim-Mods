namespace AutoGodHeal;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using Game.Actors;
using Game.Actors.Pawns;
using Game.AI;
using Game.Faith;
using HarmonyLib;
using System.Text;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger { get; private set; }

    private static ConfigEntry<bool> DisableInfection;
    private static ConfigEntry<bool> DisableInjury;
    private static ConfigEntry<bool> DisableAging;

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        Logger = base.Logger;

        // Configuration
        DisableInfection = Config.Bind(MyPluginInfo.PLUGIN_NAME, "Disable Infection", false, "Disable infection of citizens");
        DisableInjury = Config.Bind(MyPluginInfo.PLUGIN_NAME, "Disable Injury", false, "Disable injury of citizens");
        DisableAging = Config.Bind(MyPluginInfo.PLUGIN_NAME, "Disable Aging", false, "Disable aging of citizens");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    [HarmonyPatch(typeof(CitizenActor), nameof(CitizenActor.UpdateAge)), HarmonyPrefix]
    public static bool CitizenActor_UpdateAge_Prefix(CitizenActor __instance)
    {
        try
        {
            int immortalAge = __instance.OldAge - PropertiesInfo.Data.WorkerAgeProperties.AdultAge;
            if (__instance.Age >= immortalAge && DisableAging.Value)
            {
                __instance.Age = immortalAge;
                return false;
            }
        }
        catch
        {
            // Ignore
        }

        return true;
    }

    [HarmonyPatch(typeof(CitizenActor), nameof(CitizenActor.SetInfected)), HarmonyPrefix]
    public static bool CitizenActor_SetInfected_Prefix(CitizenActor __instance, bool infected, NuggetInfectionType infectionType, Actor infectionSource)
    {
        if (infected && DisableInfection.Value)
            return false;

        return true;
    }

    [HarmonyPatch(typeof(CitizenActor), nameof(CitizenActor.SetInfected)), HarmonyPostfix]
    public static void CitizenActor_SetInfected_Postfix(CitizenActor __instance, bool infected, NuggetInfectionType infectionType, Actor infectionSource)
    {
        if (!infected || DisableInfection.Value)
            return;

        Logger.LogInfo($"CitizenActor_SetInfected_Postfix: {__instance.Name} infected by {infectionSource} with {infectionType}");

        var faithController = FaithController.Instance;
        var current = faithController.CurrentGodPowerConfig;
        faithController.SetCurrentGodPower(Game.Configs.GodPowerConfig.Data.Type.Regenerate);

        if (infectionSource is StageActor stageActor && faithController.HasEnoughPowerPoints(faithController.CurrentGodPowerConfig.CreatorPowerPoints))
        {
            faithController.GodPowerWrapper.OnActionApplied(stageActor.WorldPosition, stageActor);
        }

        if (faithController.HasEnoughPowerPoints(faithController.CurrentGodPowerConfig.CreatorPowerPoints))
            faithController.GodPowerWrapper.OnActionApplied(__instance.WorldPosition, __instance);

        faithController.SetCurrentGodPower(current);
    }

    [HarmonyPatch(typeof(CitizenActor), nameof(CitizenActor.Injure)), HarmonyPrefix]
    public static bool CitizenActor_Injure_Prefix(CitizenActor __instance)
    {
        if (DisableInjury.Value)
            return false;

        return true;

    }

    [HarmonyPatch(typeof(CitizenActor), nameof(CitizenActor.Injure)), HarmonyPostfix]
    public static void CitizenActor_Injure_Postfix(CitizenActor __instance)
    {
        if (!__instance.IsInjured || DisableInjury.Value)
            return;

        var faithController = FaithController.Instance;
        var current = faithController.CurrentGodPowerConfig;
        faithController.SetCurrentGodPower(Game.Configs.GodPowerConfig.Data.Type.Regenerate);
        if (faithController.HasEnoughPowerPoints(faithController.CurrentGodPowerConfig.CreatorPowerPoints))
            faithController.GodPowerWrapper.OnActionApplied(__instance.WorldPosition, __instance);
        faithController.SetCurrentGodPower(current);
    }
}
