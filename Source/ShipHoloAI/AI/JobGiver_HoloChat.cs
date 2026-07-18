using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Sends the avatar over to a crew member for a word, on an internal cooldown.
    /// </summary>
    public class JobGiver_HoloChat : ThinkNode_JobGiver
    {
        private const int RetryIntervalTicks = 500;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar) || Find.TickManager.TicksGame < avatar.nextChatTick)
            {
                return null;
            }

            Pawn recipient = FindRecipient(avatar);
            if (recipient == null)
            {
                avatar.nextChatTick = Find.TickManager.TicksGame + RetryIntervalTicks;
                return null;
            }

            Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_Chat, recipient);
            job.locomotionUrgency = LocomotionUrgency.Amble;
            return job;
        }

        private static Pawn FindRecipient(Pawn_HoloAvatar avatar)
        {
            List<Pawn> colonists = avatar.Map.mapPawns.FreeColonistsSpawned;
            Pawn best = null;
            int bestScore = int.MinValue;
            foreach (Pawn colonist in colonists)
            {
                if (colonist.Dead || colonist.Downed || !colonist.Awake() || colonist.Drafted
                    || colonist.InMentalState
                    || avatar.Map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true
                    || !avatar.CanReach(colonist, PathEndMode.Touch, Danger.None))
                {
                    continue;
                }
                // Prefer whoever the avatar hasn't visited recently; approximate with random jitter.
                int score = Rand.Range(0, 100);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = colonist;
                }
            }
            return best;
        }
    }
}
