using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AtOTimeScale
{
    // Forces GameManager.configGameSpeed to Ultrafast. The game assigns it only in GameManager.Start,
    // SettingsManager.LoadPrefs (called once from Start) and SettingsManager.SetFastMode, so the value is
    // applied once after PrefsLoaded and restored after every SetFastMode.
    internal static class Ultrafast
    {
        private static bool resolved;
        private static bool unavailable;
        private static bool patched;
        private static bool pending;

        private static PropertyInfo instanceProperty;
        private static FieldInfo prefsLoadedField;
        private static FieldInfo configGameSpeedField;
        private static object ultrafastValue;
        private static object fastValue;
        private static object slowValue;
        private static Type settingsManagerType;

        // Called from Plugin.Awake: patches only when the option is on at startup.
        public static void Init(Harmony harmony)
        {
            if (!Plugin.EnableUltrafast.Value)
            {
                Plugin.Log.LogInfo("0 patches applied");
                return;
            }
            Enable(harmony);
        }

        public static void Enable(Harmony harmony)
        {
            if (!Resolve())
                return;
            if (!patched)
            {
                var postfix = new HarmonyMethod(AccessTools.Method(typeof(Ultrafast), nameof(SetFastModePostfix)));
                int applied = TryPatch(harmony, settingsManagerType, "SetFastMode", postfix) ? 1 : 0;
                patched = true;
                Plugin.Log.LogInfo($"{applied} patches applied");
            }
            pending = true;
        }

        public static void Disable()
        {
            pending = false;
            if (!resolved || unavailable)
                return;

            object manager = GetReadyManager();
            if (manager == null)
                return;

            bool fastMode = PlayerPrefs.HasKey("fastMode") && PlayerPrefs.GetInt("fastMode") == 1;
            configGameSpeedField.SetValue(manager, fastMode ? fastValue : slowValue);
            Plugin.Log.LogInfo($"Ultrafast off, game speed restored to {(fastMode ? "Fast" : "Slow")}");
        }

        // Called every frame from TimeScaleBehaviour.Update.
        public static void Tick()
        {
            if (!pending || unavailable)
                return;

            object manager = GetReadyManager();
            if (manager == null)
                return;

            configGameSpeedField.SetValue(manager, ultrafastValue);
            pending = false;
            Plugin.Log.LogInfo("Game speed set to Ultrafast");
        }

        private static bool Resolve()
        {
            if (resolved)
                return !unavailable;
            resolved = true;
            unavailable = !TryResolve();
            return !unavailable;
        }

        private static bool TryResolve()
        {
            Type gameManagerType = AccessTools.TypeByName("GameManager");
            if (gameManagerType == null)
                return Missing("Type GameManager");

            instanceProperty = AccessTools.Property(gameManagerType, "Instance");
            if (instanceProperty == null || instanceProperty.GetGetMethod(true)?.IsStatic != true)
                return Missing("GameManager.Instance");

            prefsLoadedField = AccessTools.Field(gameManagerType, "PrefsLoaded");
            if (prefsLoadedField == null || prefsLoadedField.FieldType != typeof(bool))
                return Missing("GameManager.PrefsLoaded");

            configGameSpeedField = AccessTools.Field(gameManagerType, "configGameSpeed");
            if (configGameSpeedField == null || !configGameSpeedField.FieldType.IsEnum)
                return Missing("GameManager.configGameSpeed");

            ultrafastValue = ParseSpeed("Ultrafast");
            fastValue = ParseSpeed("Fast");
            slowValue = ParseSpeed("Slow");
            if (ultrafastValue == null || fastValue == null || slowValue == null)
                return false;

            settingsManagerType = AccessTools.TypeByName("SettingsManager");
            if (settingsManagerType == null)
                return Missing("Type SettingsManager");
            if (AccessTools.Method(settingsManagerType, "SetFastMode", new[] { typeof(bool) }) == null)
                return Missing("SettingsManager.SetFastMode(bool)");

            return true;
        }

        private static object ParseSpeed(string name)
        {
            try
            {
                return Enum.Parse(configGameSpeedField.FieldType, name);
            }
            catch (ArgumentException)
            {
                Missing($"{configGameSpeedField.FieldType.Name}.{name}");
                return null;
            }
        }

        private static bool Missing(string what)
        {
            Plugin.Log.LogWarning($"{what} not found, Ultrafast disabled");
            return false;
        }

        // GameManager.Instance once GameManager.Start has loaded the prefs, otherwise null.
        private static object GetReadyManager()
        {
            object manager = instanceProperty.GetValue(null);
            if (!IsAlive(manager))
                return null;
            return (bool)prefsLoadedField.GetValue(manager) ? manager : null;
        }

        // Unity equality also treats a destroyed GameManager as null.
        private static bool IsAlive(object value) => value is UnityEngine.Object o ? o != null : value != null;

        private static bool TryPatch(Harmony harmony, Type type, string methodName, HarmonyMethod postfix)
        {
            MethodInfo original = AccessTools.Method(type, methodName, new[] { typeof(bool) });
            if (original == null)
            {
                Plugin.Log.LogWarning($"{type.Name}.{methodName} not found, skipping");
                return false;
            }

            try
            {
                harmony.Patch(original, postfix: postfix);
                Plugin.Log.LogInfo($"Patch applied: {type.Name}.{methodName}");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Patching {type.Name}.{methodName} failed: {e}");
                return false;
            }
        }

        private static void SetFastModePostfix()
        {
            try
            {
                if (!Plugin.EnableUltrafast.Value || unavailable)
                    return;
                object manager = instanceProperty.GetValue(null);
                if (!IsAlive(manager))
                    return;
                configGameSpeedField.SetValue(manager, ultrafastValue);
                Plugin.Log.LogDebug("SetFastMode overridden, game speed kept at Ultrafast");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"SetFastMode postfix failed: {e}");
            }
        }
    }
}
