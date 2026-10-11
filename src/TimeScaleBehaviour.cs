using System;
using UnityEngine;

namespace AtOTimeScale
{
    public class TimeScaleBehaviour : MonoBehaviour
    {
        private const float Padding = 8f;
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.7f);

        private bool updateFailed;
        private bool externalResetWarned;
        private bool overlayFailed;
        private bool overlayErrorLogged;

        private void Update()
        {
            try
            {
                // Ultrafast is governed only by EnableUltrafast, independent of Enabled.
                Ultrafast.Tick();

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

        private void OnGUI()
        {
            try
            {
                if (overlayFailed || !Plugin.Enabled.Value)
                    return;
                if (Plugin.HideAtNormalSpeed.Value && ScaleLogic.IsNormal(Plugin.CurrentScale))
                    return;

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Plugin.FontSize.Value,
                    wordWrap = false
                };

                string text = ScaleLogic.Format(Plugin.CurrentScale);
                Vector2 size = style.CalcSize(new GUIContent(text));
                float x = Plugin.PositionX.Value;
                float y = Plugin.PositionY.Value;

                Color previous = GUI.color;
                GUI.color = BackgroundColor;
                GUI.DrawTexture(new Rect(x, y, size.x + Padding * 2f, size.y + Padding * 2f), Texture2D.whiteTexture);
                GUI.color = previous;

                GUI.Label(new Rect(x + Padding, y + Padding, size.x, size.y), text, style);
            }
            catch (Exception e)
            {
                Fail("OnGUI", e);
            }
        }

        private void Fail(string where, Exception e)
        {
            if (!overlayErrorLogged)
            {
                overlayErrorLogged = true;
                Plugin.Log.LogError($"Overlay {where} failed, hiding overlay: {e}");
            }
            overlayFailed = true;
        }

        private static void Step(double target, string reason)
        {
            if (Math.Abs(target - Plugin.CurrentScale) < 1e-4)
                return;
            Plugin.ApplyScale(target, reason);
        }
    }
}
