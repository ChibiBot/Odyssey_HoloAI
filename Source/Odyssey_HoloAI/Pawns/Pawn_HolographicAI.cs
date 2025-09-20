using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public class Pawn_HolographicAI : Pawn
{
    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        map.GetComponent<GravshipMapComponent>()?.RegisterPawn(this);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        Map?.GetComponent<GravshipMapComponent>()?.UnregisterPawn(this);
        base.DeSpawn(DestroyMode.Vanish);
    }

    public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        absorbed = true;
    }

    public override void Kill(DamageInfo? dinfo, Hediff? exactCulprit = null)
    {
        // The hologram cannot be killed. Simply despawn it instead of going through the death pipeline.
        if (Spawned)
        {
            Map?.GetComponent<GravshipMapComponent>()?.NotifyPawnSuppressed(this);
            DeSpawn(DestroyMode.Vanish);
        }
    }

    public override void PostMake()
    {
        base.PostMake();
        story ??= new Pawn_StoryTracker(this);
        skills ??= new Pawn_SkillTracker(this);
        health ??= new Pawn_HealthTracker(this);
    }
}
