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

        private static readonly IntRange ChatCooldownTicks = new IntRange(2500, 7500);

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref holoCore, "holoCore");
            Scribe_Values.Look(ref nextChatTick, "nextChatTick");
        }
    }
}
