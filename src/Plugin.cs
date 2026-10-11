using BepInEx;
using BepInEx.Logging;

namespace AtOTimeScale
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            try
            {
                Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded");
            }
            catch (System.Exception e)
            {
                Log.LogError($"TimeScale failed to initialise: {e}");
            }
        }
    }
}
