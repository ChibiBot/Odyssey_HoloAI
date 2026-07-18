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
                // While the styling dialog is open, a temp story tracker holds the
                // live selection — mirror it so the render node updates in real time.
                if (story?.hairDef != null)
                {
                    return story.hairDef;
                }
                if (hairDef == null)
                {
                    hairDef = DefDatabase<HairDef>.GetNamedSilentFail("Flowy")
                        ?? DefDatabase<HairDef>.AllDefsListForReading.FirstOrDefault(h => !h.noGraphic);
                }
                return hairDef;
            }
        }

        public Color HoloHairColor
        {
            get
            {
                if (story != null)
                {
                    return story.HairColor;
                }
                return hairColorInt ?? DefaultHairColor;
            }
        }

        /// <summary>Adopt a persona's identity: name, default hair, and hair color.</summary>
        public void ApplyPersonaStyle(HoloPersonaDef persona)
        {
            Name = new NameSingle(persona.avatarName ?? persona.label);
            HairDef personaHair = persona.DefaultHair;
            if (personaHair != null)
            {
                hairDef = personaHair;
            }
            hairColorInt = persona.hairColor;
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

        /// <summary>
        /// Attach minimal story/style trackers (humanlike-only normally; the styling
        /// dialog dereferences them in its constructor) and open the instant-apply
        /// styling UI. Ideology must be active. skinColorOverride is mandatory —
        /// story.SkinColor throws on a genes-less pawn without it.
        /// </summary>
        public void OpenStylingUI()
        {
            AttachStyleTrackers();
            Find.WindowStack.Add(new Dialog_HoloStyling(this));
        }

        public void AttachStyleTrackers()
        {
            story = new Pawn_StoryTracker(this)
            {
                hairDef = CurrentHairDef,
                skinColorOverride = new Color(0.78f, 0.94f, 1f),
                bodyType = BodyTypeDefOf.Female,
                headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail("Female_AverageNormal"),
            };
            story.HairColor = HoloHairColor;
            style = new Pawn_StyleTracker(this)
            {
                beardDef = BeardDefOf.NoBeard,
            };
            if (ModsConfig.IdeologyActive)
            {
                style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
        }

        /// <summary>Copy the dialog's picks back and drop the temp trackers so they
        /// never leak into a save (Pawn.ExposeData would scribe them).</summary>
        public void DetachStyleTrackers()
        {
            if (story != null)
            {
                if (story.hairDef != null)
                {
                    hairDef = story.hairDef;
                }
                hairColorInt = story.HairColor;
            }
            story = null;
            style = null;
            Drawer.renderer.SetAllGraphicsDirty();
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
                string line = PrismSpeech.ResolveLine("bark");
                if (!line.NullOrEmpty())
                {
                    MoteMaker.ThrowText(DrawPos + new Vector3(0f, 0f, 0.65f),
                        Map, line, new Color(0f, 0.855f, 1f), 5f);
                }
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
            if (ModsConfig.IdeologyActive)
            {
                yield return new Command_Action
                {
                    defaultLabel = "HoloAI_Restyle".Translate(),
                    defaultDesc = "HoloAI_RestyleDesc".Translate(),
                    icon = holoCore?.def.uiIcon,
                    action = OpenStylingUI,
                };
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "HoloAI_Hairstyle".Translate() + ": " + (CurrentHairDef?.LabelCap ?? "-"),
                    defaultDesc = "HoloAI_HairstyleDesc".Translate(),
                    icon = holoCore?.def.uiIcon,
                    action = CycleHairstyle,
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holoCore, "holoCore");
            Scribe_Values.Look(ref nextChatTick, "nextChatTick");
            Scribe_Defs.Look(ref hairDef, "hairDef");
            Scribe_Values.Look(ref hairColorInt, "hairColor");
        }
    }
}
