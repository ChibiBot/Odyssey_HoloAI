using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// The projected persona. Intangible: absorbs all damage, cannot die while her
    /// holocore stands — lethal events fizzle her back into the core instead.
    /// </summary>
    public class Pawn_HoloAvatar : Pawn
    {
        public Building_HoloCore holoCore;
        public int nextChatTick;

        private HairDef hairDef;

        private static readonly IntRange ChatCooldownTicks = new IntRange(2500, 7500);

        // 4 long styles, 4 cute ones — all Core assets.
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
                    MoteMaker.ThrowText(DrawPos + new UnityEngine.Vector3(0f, 0f, 0.65f),
                        Map, line, new UnityEngine.Color(0f, 0.855f, 1f), 5f);
                }
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
            yield return new Command_Action
            {
                defaultLabel = "HoloAI_Hairstyle".Translate() + ": " + (CurrentHairDef?.LabelCap ?? "-"),
                defaultDesc = "HoloAI_HairstyleDesc".Translate(),
                icon = holoCore?.def.uiIcon,
                action = CycleHairstyle,
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holoCore, "holoCore");
            Scribe_Values.Look(ref nextChatTick, "nextChatTick");
            Scribe_Defs.Look(ref hairDef, "hairDef");
        }
    }
}
