using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The avatar's own styling UI — core-game content only (HairDefs + colors),
    /// no Ideology required and no Dialog_StylingStation inheritance, so every
    /// player gets the full experience and the old temp-story-tracker hack is
    /// gone entirely. Left: live hologram preview composited the same way the
    /// render tree projects her. Right: hairstyle grid, color swatches, and RGB
    /// sliders. Changes apply to the projected avatar live; Cancel restores the
    /// style she opened with.
    /// </summary>
    public class Dialog_HoloAvatarStyling : Window
    {
        // Preview layer geometry/filters mirror PawnRenderTree_Hologram.xml.
        private const float MeshSize = 1.5f;
        private const float HeadOffsetZ = 0.34f;
        private const float HairAlpha = 0.72f;
        private static readonly HoloFilter BodyFilter =
            new HoloFilter(new Color(0.55f, 0.82f, 1f), 0.95f, 0.38f);
        private static readonly HoloFilter OutfitFilter =
            new HoloFilter(new Color(0.80f, 0.93f, 1f), 0.95f, 0.30f);
        private static readonly HoloFilter HeadFilter =
            new HoloFilter(new Color(0.60f, 0.85f, 1f), 0.95f);
        private const string BodyTexPath = "Things/Pawn/Humanlike/Bodies/Naked_Female";
        private const string OutfitTexPath = "Things/Pawn/Humanlike/Apparel/ShirtButton/ShirtButton_Female";
        private const string HeadTexPath = "Things/Pawn/Humanlike/Heads/Female/Female_Average_Normal";

        private const float PreviewWidth = 230f;
        private const float HairCellSize = 64f;
        private const int HairColumns = 5;
        private const float SwatchSize = 24f;

        // Persona signature colors first, then a general photonic palette.
        private static readonly Color[] Swatches =
        {
            new Color(0.05f, 0.68f, 1f), new Color(1f, 0.72f, 0.5f),
            new Color(0.4f, 1f, 0.72f), new Color(0.72f, 0.55f, 1f),
            new Color(0.85f, 0.97f, 1f), new Color(0.82f, 0.08f, 0.12f),
            new Color(0.95f, 0.95f, 0.98f), new Color(0.75f, 0.78f, 0.82f),
            new Color(1f, 0.85f, 0.35f), new Color(1f, 0.55f, 0.15f),
            new Color(1f, 0.30f, 0.30f), new Color(1f, 0.45f, 0.85f),
            new Color(0.75f, 0.30f, 1f), new Color(0.35f, 0.40f, 1f),
            new Color(0.20f, 0.85f, 1f), new Color(0.15f, 0.95f, 0.75f),
            new Color(0.45f, 0.95f, 0.30f), new Color(0.55f, 0.40f, 0.25f),
        };

        private readonly Pawn_HoloAvatar avatar;
        private readonly HairDef originalHair;
        private readonly Color originalColor;
        private readonly List<HairDef> hairs;

        private HairDef selectedHair;
        private Color selectedColor;
        private Vector2 hairScroll;
        private bool accepted;

        public override Vector2 InitialSize => new Vector2(720f, 640f);

        public Dialog_HoloAvatarStyling(Pawn_HoloAvatar avatar)
        {
            this.avatar = avatar;
            originalHair = avatar.CurrentHairDef;
            originalColor = avatar.HoloHairColor;
            selectedHair = originalHair;
            selectedColor = originalColor;
            hairs = DefDatabase<HairDef>.AllDefsListForReading
                .Where(h => !h.noGraphic && !h.texPath.NullOrEmpty())
                .OrderBy(h => h.label)
                .ToList();
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), "HoloAI_StylingTitle".Translate());
            Text.Font = GameFont.Small;

            float top = 40f;
            float bottom = inRect.height - 40f;
            Rect previewRect = new Rect(0f, top, PreviewWidth, 330f);
            DrawPreview(previewRect);

            float rightX = PreviewWidth + 16f;
            float rightWidth = inRect.width - rightX;

            // Hairstyle grid.
            Widgets.Label(new Rect(rightX, top, rightWidth, 24f), "HoloAI_StylingHair".Translate());
            Rect gridOuter = new Rect(rightX, top + 26f, rightWidth, 304f);
            int rows = Mathf.CeilToInt(hairs.Count / (float)HairColumns);
            Rect gridView = new Rect(0f, 0f, gridOuter.width - 20f, rows * (HairCellSize + 4f));
            Widgets.BeginScrollView(gridOuter, ref hairScroll, gridView);
            for (int i = 0; i < hairs.Count; i++)
            {
                HairDef hair = hairs[i];
                Rect cell = new Rect(
                    (i % HairColumns) * (HairCellSize + 4f),
                    (i / HairColumns) * (HairCellSize + 4f),
                    HairCellSize, HairCellSize);
                Widgets.DrawBoxSolid(cell, new Color(0.10f, 0.13f, 0.18f, 0.9f));
                Texture2D tex = ContentFinder<Texture2D>.Get(hair.texPath + "_south", reportFailure: false);
                if (tex != null)
                {
                    GUI.color = new Color(selectedColor.r, selectedColor.g, selectedColor.b);
                    Widgets.DrawTextureFitted(cell.ContractedBy(2f), tex, 1f);
                    GUI.color = Color.white;
                }
                GUI.color = hair == selectedHair
                    ? new Color(0.05f, 0.68f, 1f)
                    : new Color(1f, 1f, 1f, 0.2f);
                Widgets.DrawBox(cell);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(cell, hair.LabelCap);
                if (Widgets.ButtonInvisible(cell))
                {
                    selectedHair = hair;
                    ApplyLive();
                }
            }
            Widgets.EndScrollView();

            // Color swatches.
            float swatchTop = top + 340f;
            Widgets.Label(new Rect(rightX, swatchTop, rightWidth, 24f), "HoloAI_StylingColor".Translate());
            float sy = swatchTop + 26f;
            for (int i = 0; i < Swatches.Length; i++)
            {
                Rect s = new Rect(rightX + (i % 9) * (SwatchSize + 6f),
                    sy + (i / 9) * (SwatchSize + 6f), SwatchSize, SwatchSize);
                Widgets.DrawBoxSolid(s, Swatches[i]);
                if (ColorsClose(Swatches[i], selectedColor))
                {
                    GUI.color = Color.white;
                    Widgets.DrawBox(s, 2);
                }
                if (Widgets.ButtonInvisible(s))
                {
                    selectedColor = Swatches[i];
                    ApplyLive();
                }
            }

            // RGB sliders for anything the swatches don't cover.
            float sliderTop = sy + 2f * (SwatchSize + 6f) + 10f;
            Color slid = selectedColor;
            slid.r = Widgets.HorizontalSlider(new Rect(rightX, sliderTop, rightWidth - 10f, 20f), slid.r, 0f, 1f, true, null, "R");
            slid.g = Widgets.HorizontalSlider(new Rect(rightX, sliderTop + 24f, rightWidth - 10f, 20f), slid.g, 0f, 1f, true, null, "G");
            slid.b = Widgets.HorizontalSlider(new Rect(rightX, sliderTop + 48f, rightWidth - 10f, 20f), slid.b, 0f, 1f, true, null, "B");
            if (slid != selectedColor)
            {
                selectedColor = slid;
                ApplyLive();
            }

            // Bottom buttons.
            if (Widgets.ButtonText(new Rect(0f, bottom, 140f, 32f), "CancelButton".Translate()))
            {
                RestoreOriginal();
                accepted = true; // restored already; PostClose must not double-fire
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width - 140f, bottom, 140f, 32f), "AcceptButton".Translate()))
            {
                accepted = true;
                Close();
            }
        }

        /// <summary>Composites the hologram exactly as the render tree projects
        /// her — body, outfit, head, then the SELECTED hair in the SELECTED color
        /// through the real hologram filter — so the preview is truthful.</summary>
        private void DrawPreview(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.015f, 0.04f, 0.09f, 0.9f));
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            Widgets.DrawBox(rect);
            GUI.color = Color.white;

            float size = rect.width;
            float headLift = HeadOffsetZ / MeshSize * size;
            float topPad = Mathf.Max(0f, (rect.height - (size + headLift)) / 2f);
            Rect bodyRect = new Rect(rect.x, rect.y + topPad + headLift, size, size);
            Rect headRect = new Rect(bodyRect.x, bodyRect.y - headLift, size, size);

            DrawLayer(bodyRect, BodyTexPath, BodyFilter);
            DrawLayer(bodyRect, OutfitTexPath, OutfitFilter);
            DrawLayer(headRect, HeadTexPath, HeadFilter);
            if (selectedHair != null && !selectedHair.noGraphic && !selectedHair.texPath.NullOrEmpty())
            {
                DrawLayer(headRect, selectedHair.texPath, new HoloFilter(selectedColor, HairAlpha));
            }
        }

        private static void DrawLayer(Rect rect, string texPath, HoloFilter filter)
        {
            Material mat = HoloGraphicPool.Get(texPath, filter).MatSouth;
            if (mat != null && mat.mainTexture != null)
            {
                GenUI.DrawTextureWithMaterial(rect, mat.mainTexture, mat);
            }
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
        }

        private void ApplyLive()
        {
            avatar.ApplyStyleOverride(selectedHair, selectedColor);
        }

        private void RestoreOriginal()
        {
            avatar.ApplyStyleOverride(originalHair, originalColor);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!accepted)
            {
                // Closed via X / escape: treat as cancel.
                RestoreOriginal();
            }
        }
    }
}
