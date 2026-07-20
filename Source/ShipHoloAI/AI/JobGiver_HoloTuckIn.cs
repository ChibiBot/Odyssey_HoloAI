using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// V.E.S.T.A.'s signature: she visits colonists asleep in bed on the ship and
    /// tucks them in — a small mood memory whose presence doubles as the per-colonist
    /// once-a-day cooldown (scribed and player-visible for free). No-op for every
    /// other persona; same self-gating pattern as JobGiver_HoloWarden.
    /// </summary>
    public class JobGiver_HoloTuckIn : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar)
                || avatar.holoCore?.ActivePersona != HoloAI_DefOf.HoloAI_Persona_VESTA)
            {
                return null;
            }

            foreach (Pawn colonist in avatar.Map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist.Dead || colonist.Awake() || !colonist.InBed()
                    || avatar.Map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true
                    || !avatar.CanReach(colonist, PathEndMode.Touch, Danger.None))
                {
                    continue;
                }
                // The memory IS the cooldown: still tucked in = not due again.
                if (colonist.needs?.mood?.thoughts?.memories?
                        .GetFirstMemoryOfDef(HoloAI_DefOf.HoloAI_TuckedInByVESTA) != null)
                {
                    continue;
                }
                Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_TuckIn, colonist);
                job.locomotionUrgency = LocomotionUrgency.Amble;
                return job;
            }
            return null;
        }
    }
}
