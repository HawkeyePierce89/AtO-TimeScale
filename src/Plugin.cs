using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace AtOTimeScale
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> StepsText;
        internal static ConfigEntry<float> StartupScale;
        internal static ConfigEntry<bool> RememberLastScale;
        internal static ConfigEntry<float> LastScale;
        internal static ConfigEntry<bool> EnableUltrafast;
        internal static ConfigEntry<int> PositionX;
        internal static ConfigEntry<int> PositionY;
        internal static ConfigEntry<int> FontSize;
        internal static ConfigEntry<bool> HideAtNormalSpeed;
        internal static ConfigEntry<KeyboardShortcut> SlowerKey;
        internal static ConfigEntry<KeyboardShortcut> FasterKey;
        internal static ConfigEntry<KeyboardShortcut> ResetKey;

        internal static double[] Steps = ScaleLogic.ParseSteps(ScaleLogic.DefaultSteps, out _);
        internal static double CurrentScale = 1.0;

        internal static TimeScaleBehaviour Behaviour;

        private void Awake()
        {
            Log = Logger;

            try
            {
                Enabled = Config.Bind("General", "Enabled", true,
                    "Apply the chosen time scale. When off, the game runs at normal speed.");
                StepsText = Config.Bind("General", "Steps", ScaleLogic.DefaultSteps,
                    "Comma-separated speed multipliers the Faster/Slower hotkeys step through (clamped to 0.25-20).");
                StartupScale = Config.Bind("General", "StartupScale", 1.0f,
                    new ConfigDescription("Time scale applied at game start (unless RememberLastScale is on).",
                        new AcceptableValueRange<float>((float)ScaleLogic.MinScale, (float)ScaleLogic.MaxScale)));
                RememberLastScale = Config.Bind("General", "RememberLastScale", false,
                    "Start with the last used time scale instead of StartupScale.");
                LastScale = Config.Bind("General", "LastScale", 1.0f,
                    "Last applied time scale. Written by the plugin.");
                EnableUltrafast = Config.Bind("General", "EnableUltrafast", false,
                    "Force the game's combat speed setting to Ultrafast.");
                PositionX = Config.Bind("Overlay", "PositionX", 20,
                    "Overlay left edge in screen pixels.");
                PositionY = Config.Bind("Overlay", "PositionY", 200,
                    "Overlay top edge in screen pixels.");
                FontSize = Config.Bind("Overlay", "FontSize", 18,
                    new ConfigDescription("Overlay font size.", new AcceptableValueRange<int>(10, 48)));
                HideAtNormalSpeed = Config.Bind("Overlay", "HideAtNormalSpeed", true,
                    "Hide the overlay while the time scale is x1.0.");
                SlowerKey = Config.Bind("Hotkeys", "Slower", new KeyboardShortcut(KeyCode.F6),
                    "Step to the previous (slower) time scale.");
                FasterKey = Config.Bind("Hotkeys", "Faster", new KeyboardShortcut(KeyCode.F7),
                    "Step to the next (faster) time scale.");
                ResetKey = Config.Bind("Hotkeys", "Reset", new KeyboardShortcut(KeyCode.F9),
                    "Reset the time scale to x1.0.");

                ReparseSteps();
                StepsText.SettingChanged += OnStepsChanged;
                Enabled.SettingChanged += OnEnabledChanged;

                double initial = RememberLastScale.Value ? LastScale.Value : StartupScale.Value;
                ApplyScale(initial, "startup");

                var host = new GameObject("TimeScale");
                DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;
                Behaviour = host.AddComponent<TimeScaleBehaviour>();

                Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
            }
            catch (System.Exception e)
            {
                Log.LogError($"TimeScale failed to initialise: {e}");
            }
        }

        internal static void ApplyScale(double scale, string reason)
        {
            CurrentScale = ScaleLogic.Clamp(scale);
            if (Enabled.Value)
                Time.timeScale = (float)CurrentScale;
            LastScale.Value = (float)CurrentScale;
            Log.LogInfo($"Time scale {ScaleLogic.Format(CurrentScale)} ({reason})");
        }

        private static void ReparseSteps()
        {
            Steps = ScaleLogic.ParseSteps(StepsText.Value, out bool usedDefault);
            if (usedDefault)
                Log.LogWarning($"Steps \"{StepsText.Value}\" has no valid values, using \"{ScaleLogic.DefaultSteps}\"");
        }

        private static void OnStepsChanged(object sender, System.EventArgs args)
        {
            try
            {
                ReparseSteps();
            }
            catch (System.Exception e)
            {
                Log.LogError($"Steps change handler failed: {e}");
            }
        }

        private static void OnEnabledChanged(object sender, System.EventArgs args)
        {
            try
            {
                if (Enabled.Value)
                {
                    Time.timeScale = (float)CurrentScale;
                    Log.LogInfo($"Enabled, time scale {ScaleLogic.Format(CurrentScale)}");
                }
                else
                {
                    Time.timeScale = 1f;
                    Log.LogInfo("Disabled, time scale x1.0");
                }
            }
            catch (System.Exception e)
            {
                Log.LogError($"Enabled change handler failed: {e}");
            }
        }
    }
}
