using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// H.E.R.M.E.S.'s signature: she keeps the ship's decks spotless in person,
    /// zipping to the nearest filth on substructure and scrubbing it herself. Ship
    /// only, one filth per job (the think tree immediately re-issues, so she chains
    /// them naturally), colonist-parity cleaning speed — she frees the crew from a
    /// chore rather than outperforming them. No-op for every other persona; same
    /// self-gating pattern as JobGiver_HoloWarden.
    /// </summary>
    public class JobGiver_HoloClean : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar)
                || avatar.holoCore?.ActivePersona != HoloAI_DefOf.HoloAI_Persona_HERMES)
            {
                return null;
            }

            Filth best = FindFilth(avatar);
            if (best == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_CleanFilth, best);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            return job;
        }

        /// <summary>Nearest reachable filth on the ship's own substructure.</summary>
        internal static Filth FindFilth(Pawn_HoloAvatar avatar)
        {
            Filth best = null;
            float bestDist = float.MaxValue;
            foreach (Thing thing in avatar.Map.listerThings.ThingsInGroup(ThingRequestGroup.Filth))
            {
                if (!(thing is Filth filth) || filth.Destroyed
                    || avatar.Map.terrainGrid.FoundationAt(filth.Position)?.IsSubstructure != true)
                {
                    continue;
                }
                float dist = avatar.Position.DistanceToSquared(filth.Position);
                if (dist < bestDist
                    && avatar.CanReach(filth, PathEndMode.Touch, Danger.None))
                {
                    bestDist = dist;
                    best = filth;
                }
            }
            return best;
        }
    }
}
