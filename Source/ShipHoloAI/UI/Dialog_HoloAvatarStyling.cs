using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The avatar's own styling UI — core-game content only (HairDefs, colors,
    /// BodyTypeDefs, HeadTypeDefs), no Ideology required and no
    /// Dialog_StylingStation inheritance. Left: live hologram preview composited
    /// the same way the render tree projects her, plus gender and body-type
    /// toggles. Right: hairstyle grid, face (head type) grid, color swatches and
    /// RGB sliders. Body and head lists enumerate the def databases, so modded
    /// body types and face/head packs appear automatically — the same openness
    /// Character Editor relies on. Changes apply to the projected avatar live;
    /// Cancel restores everything she opened with.
    /// </summary>
    public class Dialog_HoloAvatarStyling : Window
    {
        // Preview layer filters mirror PawnRenderTree_Hologram.xml.
        private const float MeshSize = 1.5f;
        private const float HeadOffsetZ = 0.34f;
        private const float HairAlpha = 0.72f;
        private static readonly HoloFilter BodyFilter =
            new HoloFilter(new Color(0.55f, 0.82f, 1f), 0.95f, 0.38f);
        private static readonly HoloFilter OutfitFilter =
            new HoloFilter(new Color(0.80f, 0.93f, 1f), 0.95f, 0.30f);
        private static readonly HoloFilter HeadFilter =
            new HoloFilter(new Color(0.60f, 0.85f, 1f), 0.95f);

        private const float LeftWidth = 232f;
        private const float HairCellSize = 58f;
        private const float HeadCellSize = 58f;
        private const float SwatchSize = 24f;

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
        private readonly HoloStyleMemento original = new HoloStyleMemento();
        private readonly List<HairDef> hairs;
        private readonly List<BodyTypeDef> bodyTypes;
        private List<HeadTypeDef> heads;

        private HairDef selectedHair;
        private Color selectedColor;
        private Gender selectedGender;
        private BodyTypeDef selectedBody;
        private HeadTypeDef selectedHead;
        private Vector2 hairScroll;
        private Vector2 headScroll;
        private bool accepted;

        public override Vector2 InitialSize => new Vector2(780f, 740f);

        public Dialog_HoloAvatarStyling(Pawn_HoloAvatar avatar)
        {
            this.avatar = avatar;
            original.hair = avatar.CurrentHairDef;
            original.color = avatar.HoloHairColor;
            original.gender = avatar.gender;
            original.bodyType = avatar.CurrentBodyType;
            original.headType = avatar.CurrentHeadType;
            selectedHair = original.hair;
            selectedColor = original.color;
            selectedGender = original.gender;
            selectedBody = original.bodyType;
            selectedHead = original.headType;

            hairs = DefDatabase<HairDef>.AllDefsListForReading
                .Where(h => !h.noGraphic && !h.texPath.NullOrEmpty())
                .OrderBy(h => h.label)
                .ToList();
            // The gendered core bodies are covered by the "Standard" toggle (which
            // tracks the gender switch — no female-body male or vice versa); this
            // list is the androgynous rest: core builds first, then modded ones.
            string[] coreOrder = { "Thin", "Fat", "Hulk" };
            bodyTypes = DefDatabase<BodyTypeDef>.AllDefsListForReading
                .Where(b => !b.bodyNakedGraphicPath.NullOrEmpty()
                    && b != BodyTypeDefOf.Female && b != BodyTypeDefOf.Male
                    && b.defName != "Baby" && b.defName != "Child")
                .OrderBy(b => { int i = System.Array.IndexOf(coreOrder, b.defName); return i < 0 ? 99 : i; })
                .ThenBy(b => b.defName)
                .ToList();
            RebuildHeadList();

            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        /// <summary>Every head the def database offers for the chosen gender —
        /// modded face/head packs included. Gene-locked and destroyed-head
        /// variants are skipped.</summary>
        private void RebuildHeadList()
        {
            heads = DefDatabase<HeadTypeDef>.AllDefsListForReading
                .Where(h => !h.graphicPath.NullOrEmpty()
                    && h.requiredGenes.NullOrEmpty()
                    && (h.gender == selectedGender || h.gender == Gender.None)
                    && !h.defName.Contains("Stump") && !h.defName.Contains("Skull"))
                .OrderBy(h => h.defName)
                .ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), "HoloAI_StylingTitle".Translate());
            Text.Font = GameFont.Small;

            float top = 40f;
            float bottom = inRect.height - 40f;

            // ---- Left column: preview + gender + body type ----
            Rect previewRect = new Rect(0f, top, LeftWidth, 320f);
            DrawPreview(previewRect);

            float ly = previewRect.yMax + 10f;
            Rect femaleRect = new Rect(0f, ly, LeftWidth / 2f - 3f, 30f);
            Rect maleRect = new Rect(LeftWidth / 2f + 3f, ly, LeftWidth / 2f - 3f, 30f);
            if (DrawToggleButton(femaleRect, "Female".Translate(), selectedGender == Gender.Female))
            {
                SetGender(Gender.Female);
            }
            if (DrawToggleButton(maleRect, "Male".Translate(), selectedGender == Gender.Male))
            {
                SetGender(Gender.Male);
            }

            ly += 38f;
            Widgets.Label(new Rect(0f, ly, LeftWidth, 24f), "HoloAI_StylingBody".Translate());
            ly += 24f;
            float bx = 0f;
            // "Standard" = the core body matching the gender toggle.
            string stdLabel = "HoloAI_StylingBodyStandard".Translate();
            float stdWidth = Mathf.Max(64f, Text.CalcSize(stdLabel).x + 18f);
            if (DrawToggleButton(new Rect(bx, ly, stdWidth, 28f), stdLabel,
                    selectedBody == BodyTypeDefOf.Female || selectedBody == BodyTypeDefOf.Male))
            {
                selectedBody = StandardBody;
                ApplyLive();
            }
            bx += stdWidth + 6f;
            foreach (BodyTypeDef body in bodyTypes)
            {
                float w = Mathf.Max(64f, Text.CalcSize(body.defName).x + 18f);
                if (bx + w > LeftWidth)
                {
                    bx = 0f;
                    ly += 32f;
                }
                if (DrawToggleButton(new Rect(bx, ly, w, 28f), body.defName, selectedBody == body))
                {
                    selectedBody = body;
                    ApplyLive();
                }
                bx += w + 6f;
            }

            // ---- Right column ----
            float rightX = LeftWidth + 16f;
            float rightWidth = inRect.width - rightX;

            Widgets.Label(new Rect(rightX, top, rightWidth, 24f), "HoloAI_StylingHair".Translate());
            DrawDefGrid(new Rect(rightX, top + 26f, rightWidth, 190f), hairs, ref hairScroll,
                HairCellSize, h => h.texPath, h => h.LabelCap,
                h => h == selectedHair, h => { selectedHair = h; ApplyLive(); }, tintThumb: true);

            float headTop = top + 226f;
            Widgets.Label(new Rect(rightX, headTop, rightWidth, 24f), "HoloAI_StylingFace".Translate());
            DrawDefGrid(new Rect(rightX, headTop + 26f, rightWidth, 128f), heads, ref headScroll,
                HeadCellSize, h => h.graphicPath, h => h.defName.Replace("_", " "),
                h => h == selectedHead, h => { selectedHead = h; ApplyLive(); }, tintThumb: false);

            float swatchTop = headTop + 164f;
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

            // ---- Bottom buttons ----
            if (Widgets.ButtonText(new Rect(0f, bottom, 140f, 32f), "CancelButton".Translate()))
            {
                RestoreOriginal();
                accepted = true; // already restored; PostClose must not double-fire
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.width - 140f, bottom, 140f, 32f), "AcceptButton".Translate()))
            {
                accepted = true;
                Close();
            }
        }

        private BodyTypeDef StandardBody =>
            selectedGender == Gender.Male ? BodyTypeDefOf.Male : BodyTypeDefOf.Female;

        private void SetGender(Gender gender)
        {
            if (selectedGender == gender)
            {
                return;
            }
            selectedGender = gender;
            // Standard always tracks the gender toggle (never a cross-gender core
            // body); androgynous builds (Thin/Fat/Hulk and modded) stay put.
            if (selectedBody == BodyTypeDefOf.Female || selectedBody == BodyTypeDefOf.Male)
            {
                selectedBody = StandardBody;
            }
            RebuildHeadList();
            if (!heads.Contains(selectedHead))
            {
                selectedHead = heads.FirstOrDefault(h => h.defName.Contains("Average"))
                    ?? heads.FirstOrDefault();
            }
            ApplyLive();
        }

        private delegate string PathGetter<T>(T def);

        private void DrawDefGrid<T>(Rect outer, List<T> defs, ref Vector2 scroll, float cell,
            PathGetter<T> path, System.Func<T, string> tip,
            System.Func<T, bool> isSelected, System.Action<T> select, bool tintThumb) where T : Def
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((outer.width - 20f) / (cell + 4f)));
            int rows = Mathf.CeilToInt(defs.Count / (float)columns);
            Rect view = new Rect(0f, 0f, outer.width - 20f, rows * (cell + 4f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            for (int i = 0; i < defs.Count; i++)
            {
                T def = defs[i];
                Rect cellRect = new Rect((i % columns) * (cell + 4f), (i / columns) * (cell + 4f), cell, cell);
                Widgets.DrawBoxSolid(cellRect, new Color(0.10f, 0.13f, 0.18f, 0.9f));
                Texture2D tex = ContentFinder<Texture2D>.Get(path(def) + "_south", reportFailure: false);
                if (tex != null)
                {
                    GUI.color = tintThumb
                        ? new Color(selectedColor.r, selectedColor.g, selectedColor.b)
                        : new Color(0.75f, 0.90f, 1f);
                    Widgets.DrawTextureFitted(cellRect.ContractedBy(2f), tex, 1f);
                    GUI.color = Color.white;
                }
                GUI.color = isSelected(def)
                    ? new Color(0.05f, 0.68f, 1f)
                    : new Color(1f, 1f, 1f, 0.2f);
                Widgets.DrawBox(cellRect);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(cellRect, tip(def));
                if (Widgets.ButtonInvisible(cellRect))
                {
                    select(def);
                }
            }
            Widgets.EndScrollView();
        }

        private bool DrawToggleButton(Rect rect, string label, bool active)
        {
            if (active)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.05f, 0.35f, 0.55f, 0.5f));
            }
            return Widgets.ButtonText(rect, label);
        }

        /// <summary>Composites the hologram exactly as the render tree projects
        /// her, from the SELECTED body/outfit/head/hair — the preview is truthful.</summary>
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

            DrawLayer(bodyRect, selectedBody?.bodyNakedGraphicPath, BodyFilter);
            DrawLayer(bodyRect, Pawn_HoloAvatar.OutfitPathFor(selectedBody), OutfitFilter);
            DrawLayer(headRect, selectedHead?.graphicPath, HeadFilter);
            if (selectedHair != null && !selectedHair.noGraphic && !selectedHair.texPath.NullOrEmpty())
            {
                DrawLayer(headRect, selectedHair.texPath, new HoloFilter(selectedColor, HairAlpha));
            }
        }

        private static void DrawLayer(Rect rect, string texPath, HoloFilter filter)
        {
            if (texPath.NullOrEmpty())
            {
                return;
            }
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
            avatar.SetAppearance(selectedGender, selectedBody, selectedHead);
        }

        private void RestoreOriginal()
        {
            avatar.ApplyStyleOverride(original.hair, original.color);
            avatar.SetAppearance(original.gender, original.bodyType, original.headType);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!accepted)
            {
                RestoreOriginal();
            }
        }
    }
}
