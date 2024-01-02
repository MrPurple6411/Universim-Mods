namespace Managers_Unleashed
{
    using BepInEx;
    using BepInEx.Logging;
    using Game.Actors.Stats;
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

        [HarmonyPatch(typeof(EmploymentCenterActor), nameof(EmploymentCenterActor.TryUnlockSlot))]
        [HarmonyPostfix]
        public static void GetAvailableManagers_Postfix(EmploymentCenterActor __instance, BuildingStatType statType)
        {
            if (!__instance.ministersSlotsTypes.Contains(statType) || __instance.unlockedSlots.Contains(statType))
            {
                return;
            }

            __instance.unlockedSlots.Add(statType);
            if (__instance.onSlotUnlocked != null)
            {
                __instance.onSlotUnlocked(statType);
            }
        }
    }
}
