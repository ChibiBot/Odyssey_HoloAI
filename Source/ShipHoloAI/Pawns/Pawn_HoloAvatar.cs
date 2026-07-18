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
        }
    }
}
