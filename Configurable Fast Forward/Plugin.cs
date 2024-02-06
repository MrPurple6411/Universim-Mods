namespace Configurable_Fast_Forward;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game;
using Game.Services;
using Game.UI;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private static Plugin Instance { get; set; }
    internal static ManualLogSource Log => Instance.Logger;

    private static ConfigEntry<float> _pauseSpeed;
    private static ConfigEntry<float> _normalSpeed;
    private static ConfigEntry<float> _fastForwardSpeed;
    private static ConfigEntry<float> _fastForwardSpeed2;
    private static ConfigEntry<KeyCode> _speedPulseHotkey;
    private static ConfigEntry<bool> _pauseOnQuests;

    public Plugin()
    {
        Instance = this;
    }

    private void Awake()
    {
        if (!Analytics.AnalyticsDisabled)
            Analytics.DisableAnalytics();

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(Plugin), MyPluginInfo.PLUGIN_GUID);

        // Plugin startup logic
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private int _lastTimeScale = -1;

    private void Update()
    {
        if (MonoSingleton<UIController>.Instance is { } controller && controller.SimControls is { } simControls)
        {
            if (Input.GetKeyDown(_speedPulseHotkey.Value))
            {
                _lastTimeScale = simControls._gameSpeed;
                simControls.SetGameSpeedState(3);
            }
            else if (Input.GetKeyUp(_speedPulseHotkey.Value) && _lastTimeScale >= 0)
            {
                simControls.SetGameSpeedState(_lastTimeScale);
            }
        }
    }

    private static bool registered = false;

    [HarmonyPatch(typeof(SimulationControls), nameof(SimulationControls.Start)), HarmonyPostfix]
    public static void SimulationControls_Start_Postfix(SimulationControls __instance)
    {
        if (registered)
        {
            __instance._pauseSpeed = _pauseSpeed.Value;
            __instance._normalSpeed = _normalSpeed.Value;
            __instance._doubleSpeed = _fastForwardSpeed.Value;
            __instance._quadSpeed = _fastForwardSpeed2.Value;
            return;
        }

        var pSpeed = __instance._pauseSpeed;
        var nSpeed = __instance._normalSpeed;
        var dSpeed = __instance._doubleSpeed;
        var qSpeed = __instance._quadSpeed;

        Log.LogDebug($"Original Pause speed: {pSpeed}");
        Log.LogDebug($"Original Normal speed: {nSpeed}");
        Log.LogDebug($"Original Fast forward speed: {dSpeed}");
        Log.LogDebug($"Original Fast forward speed 2: {qSpeed}");

        _pauseSpeed = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "0_Pause", pSpeed, "Pause");
        _pauseSpeed.SettingChanged += (sender, args) =>
        {
            if (MonoSingleton<UIController>.Instance != null)
            {
                SimulationControls simControls = MonoSingleton<UIController>.Instance.SimControls;
                simControls._pauseSpeed = _pauseSpeed.Value;
                simControls.SetGameSpeedState(simControls._gameSpeed);
                Log.LogDebug($"Pause speed changed to {_pauseSpeed.Value}");
            }
        };

        _normalSpeed = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "1_Normal", nSpeed, "Normal");
        _normalSpeed.SettingChanged += (sender, args) =>
        {
            if (MonoSingleton<UIController>.Instance != null)
            {
                SimulationControls simControls = MonoSingleton<UIController>.Instance.SimControls;
                simControls._normalSpeed = _normalSpeed.Value;
                simControls.SetGameSpeedState(simControls._gameSpeed);
                Log.LogDebug($"Normal speed changed to {_normalSpeed.Value}");
            }
        };

        _fastForwardSpeed = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "2_FastForward", dSpeed, "Fast Forward");
        _fastForwardSpeed.SettingChanged += (sender, args) =>
        {
            if (MonoSingleton<UIController>.Instance != null)
            {
                SimulationControls simControls = MonoSingleton<UIController>.Instance.SimControls;
                simControls._doubleSpeed = _fastForwardSpeed.Value;
                simControls.SetGameSpeedState(simControls._gameSpeed);
                Log.LogDebug($"Fast forward speed changed to {_fastForwardSpeed.Value}");
            }
        };

        _fastForwardSpeed2 = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "3_DoubleFastForward", qSpeed, "Double Fast Forward");
        _fastForwardSpeed2.SettingChanged += (sender, args) =>
        {
            if (MonoSingleton<UIController>.Instance != null)
            {
                SimulationControls simControls = MonoSingleton<UIController>.Instance.SimControls;
                simControls._quadSpeed = _fastForwardSpeed2.Value;
                simControls.SetGameSpeedState(simControls._gameSpeed);
                Log.LogDebug($"Fast forward speed 2 changed to {_fastForwardSpeed2.Value}");
            }
        };

        _pauseOnQuests = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "4_PauseOnQuests", false, "Pause game when a quest is available");
        _pauseOnQuests.SettingChanged += (sender, args) =>
        {
            Log.LogDebug($"Pause on quests changed to {_pauseOnQuests.Value}");
        };

        _speedPulseHotkey = Instance.Config.Bind(MyPluginInfo.PLUGIN_NAME, "5_SpeedPulseHotkey", KeyCode.V, "Hotkey to pulse Double Fast Forward while pressed");
        _speedPulseHotkey.SettingChanged += (sender, args) =>
        {
            Log.LogDebug($"Speed pulse hotkey changed to {_speedPulseHotkey.Value}");
        };

        __instance._pauseSpeed = _pauseSpeed.Value;
        __instance._normalSpeed = _normalSpeed.Value;
        __instance._doubleSpeed = _fastForwardSpeed.Value;
        __instance._quadSpeed = _fastForwardSpeed2.Value;

        registered = true;
    }

    //TODO: create patch to pause the game when a quest is available.
}
