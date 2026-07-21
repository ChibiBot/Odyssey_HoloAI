using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The persona selection screen: a codex of EVERY HoloPersonaDef, not just the
    /// matrices currently aboard. Each row shows a live-composited hologram portrait
    /// (the same body/outfit/head/hair layers and filters the render tree uses, so
    /// the preview matches the projected avatar exactly), the manufacturer's catalog
    /// blurb, the persona's own bio, and the stat buffs read generically off her
    /// <see cref="HoloPersonaDef.auraHediff"/> stages — new personas need no UI work.
    ///
    /// Row states: ACTIVE (resident now), IN ARCHIVE / CORE-RESIDENT (installed or
    /// built-in — click switches instantly, no matrix needed), MATRIX ABOARD (a
    /// matrix is on the map; click sends a colonist to install it, consuming the
    /// matrix and archiving the persona forever), MATRIX NOT DETECTED (dimmed;
    /// doubles as a trader shopping list).
    /// </summary>
    public class Dialog_PersonaSwitcher : Window
    {
        private const float PortraitWidth = 116f;
        private const float RowInnerPadding = 10f;
        private const float RowSpacing = 10f;
        private const float SegmentGap = 4f;
        private const float ScrollBarAllowance = 20f;

        // Portrait layer geometry/filters mirror PawnRenderTree_Hologram.xml — keep
        // the two in sync or the preview drifts from the in-game avatar.
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

        private static readonly Color PositiveColor = new Color(0.3f, 1f, 0.3f);
        private static readonly Color NegativeColor = ColorLibrary.RedReadable;
        private static readonly Color RowBorderColor = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color ActiveBorderColor = new Color(0.05f, 0.68f, 1f, 0.8f);
        private static readonly Color PortraitBackColor = new Color(0.015f, 0.04f, 0.09f, 0.9f);
        private static readonly Color ManufacturerColor = new Color(0.52f, 0.62f, 0.74f);
        private static readonly Color BioColor = new Color(0.85f, 0.92f, 1f);
        private static readonly Color StoryColor = new Color(0.60f, 0.63f, 0.68f);
        private static readonly Color StatusActiveColor = new Color(0.3f, 1f, 0.5f);
        private static readonly Color StatusReadyColor = new Color(0.05f, 0.82f, 1f);
        private static readonly Color StatusMissingColor = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color MissingOverlayColor = new Color(0.01f, 0.02f, 0.04f, 0.55f);
        private static readonly Color AbilityFlavorColor = new Color(0.85f, 0.76f, 0.55f);
        private static readonly Color DevStatColor = new Color(0.55f, 0.55f, 0.58f);

        private enum RowStatus { Active, Archived, MatrixAboard, Missing }

        private readonly Building_HoloCore core;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(760f, 700f);

        public Dialog_PersonaSwitcher(Building_HoloCore core)
        {
            this.core = core;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 34f);
            Widgets.Label(titleRect, "HoloAI_PersonaSwitcherTitle".Translate());
            Text.Font = GameFont.Small;

            float listTop = titleRect.height + 6f;
            Rect listRect = new Rect(0f, listTop, inRect.width, inRect.height - listTop);
            float rowWidth = listRect.width - ScrollBarAllowance;

            // Which personas have a matrix aboard right now (first matching item wins).
            var matrixFor = new Dictionary<HoloPersonaDef, Thing>();
            foreach (Thing thing in core?.GetInstallableMatrices() ?? System.Array.Empty<Thing>())
            {
                HoloPersonaDef persona = thing.def.GetModExtension<HoloPersonaMatrixExtension>()?.persona;
                if (persona != null && !matrixFor.ContainsKey(persona))
                {
                    matrixFor[persona] = thing;
                }
            }

            // Two-pass layout: measure every row's (variable) height first so the
            // scrollview's content rect and each row's draw position are known before
            // any GUI drawing happens.
            var entries = new List<(HoloPersonaDef persona, Thing matrix, RowStatus status,
                List<(string text, Color color)> statLines, float height)>();
            float totalHeight = 0f;
            foreach (HoloPersonaDef persona in DefDatabase<HoloPersonaDef>.AllDefsListForReading)
            {
                Thing matrix = matrixFor.TryGetValue(persona, out Thing found) ? found : null;
                RowStatus status = StatusFor(persona, matrix);
                List<(string text, Color color)> statLines = BuildStatLines(persona);
                float height = MeasureRowHeight(persona, statLines, rowWidth);
                entries.Add((persona, matrix, status, statLines, height));
                totalHeight += height + RowSpacing;
            }

            Rect viewRect = new Rect(0f, 0f, rowWidth, totalHeight);
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            // Portraits draw via Graphics.DrawTexture, which ignores the scrollview's
            // clip rect (only its offset) — cull/crop against the viewport ourselves
            // or off-screen rows paint their holograms onto the map around the window.
            Rect visibleRect = new Rect(0f, scrollPosition.y, rowWidth, listRect.height);
            float curY = 0f;
            foreach (var entry in entries)
            {
                Rect rowRect = new Rect(0f, curY, rowWidth, entry.height);
                if (rowRect.Overlaps(visibleRect))
                {
                    DrawRow(rowRect, entry.persona, entry.matrix, entry.status, entry.statLines,
                        visibleRect);
                }
                curY += entry.height + RowSpacing;
            }
            Widgets.EndScrollView();
        }

        private RowStatus StatusFor(HoloPersonaDef persona, Thing matrix)
        {
            if (persona == core.ActivePersona)
            {
                return RowStatus.Active;
            }
            if (core.IsUnlocked(persona))
            {
                return RowStatus.Archived;
            }
            return matrix != null ? RowStatus.MatrixAboard : RowStatus.Missing;
        }

        private void DrawRow(Rect rowRect, HoloPersonaDef persona, Thing matrix, RowStatus status,
            List<(string text, Color color)> statLines, Rect visibleRect)
        {
            bool selectable = status == RowStatus.MatrixAboard || status == RowStatus.Archived;
            if (selectable)
            {
                Widgets.DrawHighlightIfMouseover(rowRect);
            }
            GUI.color = status == RowStatus.Active ? ActiveBorderColor : RowBorderColor;
            Widgets.DrawBox(rowRect);
            GUI.color = Color.white;

            Rect inner = rowRect.ContractedBy(RowInnerPadding);
            Rect portraitRect = new Rect(inner.x, inner.y, PortraitWidth, inner.height);
            DrawPortrait(portraitRect, persona, visibleRect);

            float textX = portraitRect.xMax + RowInnerPadding;
            float textWidth = inner.xMax - textX;
            float y = inner.y;

            // Name + status chip on one line.
            Text.Font = GameFont.Medium;
            string name = persona.label.CapitalizeFirst();
            float nameHeight = Text.CalcHeight(name, textWidth);
            Widgets.Label(new Rect(textX, y, textWidth, nameHeight), name);
            Text.Font = GameFont.Tiny;
            (string statusText, Color statusColor) = StatusChip(status, persona);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = statusColor;
            Widgets.Label(new Rect(textX, y, textWidth, nameHeight), statusText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += nameHeight;

            y = DrawSegment(persona.manufacturer, GameFont.Tiny, ManufacturerColor, textX, y, textWidth, 0f);
            y = DrawSegment(persona.bio, GameFont.Small, BioColor, textX, y, textWidth, SegmentGap);
            y = DrawSegment(persona.manufacturerStory, GameFont.Tiny, StoryColor, textX, y, textWidth, SegmentGap);

            if (statLines.Count > 0)
            {
                y += SegmentGap;
                Text.Font = GameFont.Tiny;
                foreach ((string text, Color color) line in statLines)
                {
                    float lineHeight = Text.CalcHeight(line.text, textWidth);
                    GUI.color = line.color;
                    Widgets.Label(new Rect(textX, y, textWidth, lineHeight), line.text);
                    GUI.color = Color.white;
                    y += lineHeight;
                }
            }
            Text.Font = GameFont.Small;

            if (status == RowStatus.Missing)
            {
                Widgets.DrawBoxSolid(rowRect, MissingOverlayColor);
                TooltipHandler.TipRegion(rowRect, "HoloAI_PersonaMissingTip".Translate(persona.label));
            }
            else if (selectable)
            {
                TooltipHandler.TipRegion(rowRect, status == RowStatus.Archived
                    ? "HoloAI_PersonaActivateTip".Translate(persona.label)
                    : "HoloAI_PersonaInstallTip".Translate(persona.label));
                if (Widgets.ButtonInvisible(rowRect))
                {
                    if (status == RowStatus.Archived)
                    {
                        core.ActivatePersona(persona);
                    }
                    else
                    {
                        core.OrderInstall(matrix);
                    }
                    Close();
                }
            }
        }

        /// <summary>
        /// Composites the persona's hologram exactly as the render tree would project
        /// her: body, outfit, head, and her default hair in her signature color, all
        /// south-facing. Layer rects are squares the size of the in-game mesh, with
        /// the head/hair square lifted by the tree's head offset.
        /// </summary>
        private static void DrawPortrait(Rect portraitRect, HoloPersonaDef persona, Rect visibleRect)
        {
            Widgets.DrawBoxSolid(portraitRect, PortraitBackColor);
            GUI.color = RowBorderColor;
            Widgets.DrawBox(portraitRect);
            GUI.color = Color.white;

            float size = portraitRect.width;
            float headLift = HeadOffsetZ / MeshSize * size;
            // Figure spans body square + head lift; center that block vertically.
            float figureHeight = size + headLift;
            float topPad = Mathf.Max(0f, (portraitRect.height - figureHeight) / 2f);
            Rect bodyRect = new Rect(portraitRect.x, portraitRect.y + topPad + headLift, size, size);
            Rect headRect = new Rect(bodyRect.x, bodyRect.y - headLift, size, size);

            DrawLayer(bodyRect, BodyTexPath, BodyFilter, visibleRect);
            DrawLayer(bodyRect, OutfitTexPath, OutfitFilter, visibleRect);
            DrawLayer(headRect, HeadTexPath, HeadFilter, visibleRect);
            HairDef hair = persona.DefaultHair;
            if (hair != null && !hair.noGraphic && !hair.texPath.NullOrEmpty())
            {
                DrawLayer(headRect, hair.texPath,
                    new HoloFilter(persona.hairColor, HairAlpha), visibleRect);
            }
        }

        private static void DrawLayer(Rect rect, string texPath, HoloFilter filter, Rect visibleRect)
        {
            Material mat = HoloGraphicPool.Get(texPath, filter).MatSouth;
            if (mat == null || mat.mainTexture == null)
            {
                return;
            }
            // Graphics.DrawTexture ignores the scrollview clip rect, so crop the layer
            // to the viewport by hand, trimming the UVs to match (v runs bottom-up).
            Rect clipped = Rect.MinMaxRect(
                Mathf.Max(rect.xMin, visibleRect.xMin), Mathf.Max(rect.yMin, visibleRect.yMin),
                Mathf.Min(rect.xMax, visibleRect.xMax), Mathf.Min(rect.yMax, visibleRect.yMax));
            if (clipped.width <= 0f || clipped.height <= 0f)
            {
                return;
            }
            float cutLeft = (clipped.xMin - rect.xMin) / rect.width;
            float cutTop = (clipped.yMin - rect.yMin) / rect.height;
            float cutBottom = (rect.yMax - clipped.yMax) / rect.height;
            Rect texCoords = new Rect(cutLeft, cutBottom,
                clipped.width / rect.width, 1f - cutTop - cutBottom);
            GenUI.DrawTextureWithMaterial(clipped, mat.mainTexture, mat, texCoords);
        }

        private static (string, Color) StatusChip(RowStatus status, HoloPersonaDef persona)
        {
            switch (status)
            {
                case RowStatus.Active:
                    return ("HoloAI_PersonaStatusActive".Translate(), StatusActiveColor);
                case RowStatus.MatrixAboard:
                    return ("HoloAI_PersonaStatusReady".Translate(), StatusReadyColor);
                case RowStatus.Archived:
                    return (persona.matrixItem == null
                        ? "HoloAI_PersonaStatusBuiltIn".Translate()
                        : "HoloAI_PersonaStatusArchived".Translate(), StatusReadyColor);
                default:
                    return ("HoloAI_PersonaStatusMissing".Translate(), StatusMissingColor);
            }
        }

        private static float DrawSegment(string text, GameFont font, Color color,
            float x, float y, float width, float gap)
        {
            if (text.NullOrEmpty())
            {
                return y;
            }
            y += gap;
            Text.Font = font;
            float height = Text.CalcHeight(text, width);
            GUI.color = color;
            Widgets.Label(new Rect(x, y, width, height), text);
            GUI.color = Color.white;
            return y + height;
        }

        private static float MeasureSegment(string text, GameFont font, float width, float gap)
        {
            if (text.NullOrEmpty())
            {
                return 0f;
            }
            Text.Font = font;
            return gap + Text.CalcHeight(text, width);
        }

        private static float MeasureRowHeight(HoloPersonaDef persona,
            List<(string text, Color color)> statLines, float rowWidth)
        {
            float textWidth = rowWidth - RowInnerPadding * 3f - PortraitWidth;
            Text.Font = GameFont.Medium;
            float height = Text.CalcHeight(persona.label.CapitalizeFirst(), textWidth);
            height += MeasureSegment(persona.manufacturer, GameFont.Tiny, textWidth, 0f);
            height += MeasureSegment(persona.bio, GameFont.Small, textWidth, SegmentGap);
            height += MeasureSegment(persona.manufacturerStory, GameFont.Tiny, textWidth, SegmentGap);
            if (statLines.Count > 0)
            {
                height += SegmentGap;
                Text.Font = GameFont.Tiny;
                foreach ((string text, Color color) line in statLines)
                {
                    height += Text.CalcHeight(line.text, textWidth);
                }
            }
            Text.Font = GameFont.Small;
            // Portrait wants room for the figure; text usually exceeds this anyway.
            float minPortraitHeight = PortraitWidth * (1f + HeadOffsetZ / MeshSize);
            return Mathf.Max(height, minPortraitHeight) + RowInnerPadding * 2f;
        }

        /// <summary>
        /// The row's ability block: the persona's signature-ability flavor lines
        /// first (catalog gold), then every stat offset/factor read generically off
        /// the aura hediff's stages as "Stat Label +value" / "Stat Label x1.15",
        /// colored by whether the modifier helps (green) or hurts (red). Works for
        /// any persona, present or future, without per-persona code.
        ///
        /// Avatar-targeted auras (auraTargetsAvatar, e.g. I.X.I.A.) carry internal
        /// calibration constants, not crew effects — dumped raw they read as
        /// shipwide penalties ("Suppression power -72%"), so the numbers stay
        /// obfuscated behind the flavor lines unless dev mode is on, where they
        /// show grayed with an "(avatar-only calibration)" tag.
        /// </summary>
        private static List<(string text, Color color)> BuildStatLines(HoloPersonaDef persona)
        {
            var lines = new List<(string, Color)>();
            if (persona.abilityFlavor != null)
            {
                foreach (string flavor in persona.abilityFlavor)
                {
                    if (!flavor.NullOrEmpty())
                    {
                        lines.Add((flavor, AbilityFlavorColor));
                    }
                }
            }
            HediffDef aura = persona.auraHediff;
            if (aura?.stages == null)
            {
                return lines;
            }
            bool avatarOnly = persona.auraTargetsAvatar;
            if (avatarOnly && !Prefs.DevMode)
            {
                return lines;
            }
            string devSuffix = avatarOnly ? " (avatar-only calibration)" : string.Empty;
            foreach (HediffStage stage in aura.stages)
            {
                if (stage.statOffsets != null)
                {
                    foreach (StatModifier modifier in stage.statOffsets)
                    {
                        if (modifier?.stat == null)
                        {
                            continue;
                        }
                        Color color = avatarOnly ? DevStatColor
                            : modifier.value >= 0f ? PositiveColor : NegativeColor;
                        lines.Add((modifier.stat.LabelCap + " "
                            + modifier.ValueToStringAsOffset + devSuffix, color));
                    }
                }
                if (stage.statFactors != null)
                {
                    foreach (StatModifier modifier in stage.statFactors)
                    {
                        if (modifier?.stat == null)
                        {
                            continue;
                        }
                        Color color = avatarOnly ? DevStatColor
                            : modifier.value >= 1f ? PositiveColor : NegativeColor;
                        lines.Add((modifier.stat.LabelCap + " "
                            + modifier.ToStringAsFactor + devSuffix, color));
                    }
                }
            }
            return lines;
        }
    }
}
