using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// Vanilla MoteText draws GameFont.Tiny with no outline, which is nearly
    /// unreadable over bright terrain — and persona-tinted colors made it worse.
    /// Same mote lifecycle, but drawn in GameFont.Small over a four-pass black
    /// outline, so every persona tint stays legible on any ground.
    /// </summary>
    public class MoteSpeechText : MoteText
    {
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, -1f), new Vector2(0f, 1f),
        };

        public override void DrawGUIOverlay()
        {
            float alpha = Mathf.Clamp01(1f - (AgeSecs - TimeBeforeStartFadeout) / def.mote.fadeOutTime);
            if (alpha <= 0f)
            {
                return;
            }
            Vector2 screenPos = (Vector2)(Find.Camera.WorldToScreenPoint(
                new Vector3(exactPosition.x, 0f, exactPosition.z)) / Prefs.UIScale);
            screenPos.y = UI.screenHeight - screenPos.y;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            float width = Text.CalcSize(text).x;
            Rect rect = new Rect(screenPos.x - width / 2f, screenPos.y - 2f, width, 999f);

            GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            foreach (Vector2 offset in OutlineOffsets)
            {
                Widgets.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), text);
            }
            GUI.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
            Widgets.Label(rect, text);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
