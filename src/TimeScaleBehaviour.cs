using System;
using UnityEngine;

namespace AtOTimeScale
{
    public class TimeScaleBehaviour : MonoBehaviour
    {
        private bool updateFailed;
        private bool externalResetWarned;

        private void Update()
        {
            try
            {
                if (!Plugin.Enabled.Value)
                    return;

                double current = Plugin.CurrentScale;
                if (Plugin.FasterKey.Value.IsDown())
                    Step(ScaleLogic.Next(Plugin.Steps, current), "faster");
                else if (Plugin.SlowerKey.Value.IsDown())
                    Step(ScaleLogic.Previous(Plugin.Steps, current), "slower");
                else if (Plugin.ResetKey.Value.IsDown())
                    Step(1.0, "reset");

                if (Mathf.Abs(Time.timeScale - (float)Plugin.CurrentScale) > 1e-4f)
                {
                    if (!externalResetWarned)
                    {
                        externalResetWarned = true;
                        Plugin.Log.LogWarning(
                            $"Time.timeScale was changed externally to {Time.timeScale}, reapplying {ScaleLogic.Format(Plugin.CurrentScale)}");
                    }
                    Time.timeScale = (float)Plugin.CurrentScale;
                }
            }
            catch (Exception e)
            {
                if (!updateFailed)
                {
                    updateFailed = true;
                    Plugin.Log.LogError($"TimeScale Update failed: {e}");
                }
            }
        }

        private static void Step(double target, string reason)
        {
            if (Math.Abs(target - Plugin.CurrentScale) < 1e-4)
                return;
            Plugin.ApplyScale(target, reason);
        }
    }
}
