using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The projected persona. Intangible: absorbs all damage, cannot die while her
    /// holocore stands — lethal events fizzle her back into the core instead. Bound to
    /// the gravship: five seconds off substructure and she flickers back to her core.
    /// </summary>
    public class Pawn_HoloAvatar : Pawn
    {
        public Building_HoloCore holoCore;
        public int nextChatTick;

        private HairDef hairDef;
        private Color? hairColorInt;
        private BodyTypeDef bodyTypeInt;
        private HeadTypeDef headTypeInt;
        private int offShipTicks;

        private static readonly IntRange ChatCooldownTicks = new IntRange(2500, 7500);

        private const int OffShipGraceTicks = 300; // 5 s

        public static readonly Color DefaultHairColor = new Color(0.05f, 0.68f, 1f);

        // 4 long styles, 4 cute ones — all Core assets. Used by the no-Ideology
        // cycle gizmo; with Ideology the styling dialog offers the full catalog.
        private static readonly string[] CuratedHairDefNames =
        {
            "Long", "Flowy", "Princess", "Senorita",
            "Cute", "Bob", "Pigtails", "FancyBun",
        };

        public HairDef CurrentHairDef
        {
            get
            {
                if (hairDef == null)
                {
                    hairDef = DefDatabase<HairDef>.GetNamedSilentFail("Flowy")
                        ?? DefDatabase<HairDef>.AllDefsListForReading.FirstOrDefault(h => !h.noGraphic);
                }
                return hairDef;
            }
        }

        public Color HoloHairColor => hairColorInt ?? DefaultHairColor;

        public BodyTypeDef CurrentBodyType => bodyTypeInt ?? BodyTypeDefOf.Female;

        public HeadTypeDef CurrentHeadType => headTypeInt
            ?? DefDatabase<HeadTypeDef>.GetNamedSilentFail("Female_AverageNormal");

        /// <summary>Worn outfit variant for a body type; vanilla ships ShirtButton
        /// for every core body, modded body types without a variant fall back to
        /// the female cut.</summary>
        public static string OutfitPathFor(BodyTypeDef body)
        {
            string path = "Things/Pawn/Humanlike/Apparel/ShirtButton/ShirtButton_"
                + (body?.defName ?? "Female");
            if (ContentFinder<Texture2D>.Get(path + "_south", reportFailure: false) == null)
            {
                path = "Things/Pawn/Humanlike/Apparel/ShirtButton/ShirtButton_Female";
            }
            return path;
        }

        /// <summary>Set gender/body/head together (the styling dialog's appearance
        /// tab). Gender flips grammar in every interaction and announcement for
        /// free; body and head resolve through the render tree's holoPart nodes.</summary>
        public void SetAppearance(Gender newGender, BodyTypeDef body, HeadTypeDef head)
        {
            gender = newGender;
            bodyTypeInt = body;
            headTypeInt = head;
            if (Spawned)
            {
                Drawer.renderer.SetAllGraphicsDirty();
            }
        }

        /// <summary>Adopt a persona's identity: name, default hair/color, and the
        /// canon appearance (female, default body and head) — a remembered player
        /// override is reapplied on top by the core afterwards.</summary>
        public void ApplyPersonaStyle(HoloPersonaDef persona)
        {
            Name = new NameSingle(persona.avatarName ?? persona.label);
            HairDef personaHair = persona.DefaultHair;
            if (personaHair != null)
            {
                hairDef = personaHair;
            }
            hairColorInt = persona.hairColor;
            gender = Gender.Female;
            bodyTypeInt = null;
            headTypeInt = null;
            if (Spawned)
            {
                Drawer.renderer.SetAllGraphicsDirty();
            }
        }

        /// <summary>Restore a player restyle remembered by the core — overrides the
        /// persona defaults ApplyPersonaStyle just set (see Building_HoloCore's
        /// per-persona style memory).</summary>
        public void ApplyStyleOverride(HairDef hair, Color color)
        {
            if (hair != null)
            {
                hairDef = hair;
            }
            hairColorInt = color;
            if (Spawned)
            {
                Drawer.renderer.SetAllGraphicsDirty();
            }
        }

        public void CycleHairstyle()
        {
            int current = System.Array.IndexOf(CuratedHairDefNames, CurrentHairDef?.defName);
            for (int offset = 1; offset <= CuratedHairDefNames.Length; offset++)
            {
                HairDef next = DefDatabase<HairDef>.GetNamedSilentFail(
                    CuratedHairDefNames[(current + offset) % CuratedHairDefNames.Length]);
                if (next != null && !next.noGraphic)
                {
                    hairDef = next;
                    break;
                }
            }
            Drawer.renderer.SetAllGraphicsDirty();
        }

        /// <summary>Open the custom styling UI — core-game hair defs and colors
        /// only, so it works identically with or without Ideology and needs none
        /// of the old temp story-tracker machinery.</summary>
        public void OpenStylingUI()
        {
            Find.WindowStack.Add(new Dialog_HoloAvatarStyling(this));
        }

        public void SetChatCooldown()
        {
            nextChatTick = Find.TickManager.TicksGame + ChatCooldownTicks.RandomInRange;
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned)
            {
                return;
            }
            CheckShipBound();
            // Soft photonic shimmer, and the occasional idle remark.
            if (this.IsHashIntervalTick(900))
            {
                FleckMaker.ThrowLightningGlow(DrawPos, Map, 0.35f);
            }
            if (this.IsHashIntervalTick(2500) && Rand.Chance(0.3f) && !Position.Fogged(Map))
            {
                PrismSpeech.Bark(this, "bark");
            }
        }

        private void CheckShipBound()
        {
            if (Map.terrainGrid.FoundationAt(Position)?.IsSubstructure == true)
            {
                offShipTicks = 0;
                return;
            }
            offShipTicks++;
            if (offShipTicks < OffShipGraceTicks)
            {
                return;
            }
            offShipTicks = 0;

            if (holoCore == null || !holoCore.Spawned || holoCore.Map != Map)
            {
                holoCore?.StoreAvatar();
                return;
            }
            FleckMaker.ThrowLightningGlow(DrawPos, Map, 0.8f);
            if (CellFinder.TryFindRandomCellNear(holoCore.Position, Map, 3,
                    c => c.Standable(Map)
                        && Map.terrainGrid.FoundationAt(c)?.IsSubstructure == true,
                    out IntVec3 cell))
            {
                jobs?.StopAll();
                Position = cell;
                Notify_Teleported();
                FleckMaker.ThrowLightningGlow(DrawPos, Map, 0.8f);
            }
            else
            {
                holoCore.StoreAvatar();
            }
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = true;
            FleckMaker.ThrowLightningGlow(DrawPos, MapHeld, 0.5f);
        }

        public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            // A hologram cannot be killed; worst case she flickers back into the core.
            if (holoCore != null && !holoCore.Destroyed)
            {
                holoCore.StoreAvatar();
                return;
            }
            if (Spawned)
            {
                Destroy();
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            // One styling UI for everyone — core hair defs + colors, no DLC gate.
            yield return new Command_Action
            {
                defaultLabel = "HoloAI_Restyle".Translate(),
                defaultDesc = "HoloAI_RestyleDesc".Translate(),
                icon = HoloAIIcons.Restyle,
                action = OpenStylingUI,
            };
            // Warden-specialist personas (I.X.I.A.) personally handle prisoner
            // recruitment and slave suppression instead of buffing crew via aura —
            // JobGiver_HoloWarden fires this automatically the instant a target is
            // due, no manual click-target step.
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holoCore, "holoCore");
            Scribe_Values.Look(ref nextChatTick, "nextChatTick");
            Scribe_Defs.Look(ref hairDef, "hairDef");
            Scribe_Values.Look(ref hairColorInt, "hairColor");
            Scribe_Defs.Look(ref bodyTypeInt, "bodyType");
            Scribe_Defs.Look(ref headTypeInt, "headType");
        }
    }
}
