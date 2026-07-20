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
            // Companion triage is P.R.I.S.M.'s signature: she seeks out whoever is
            // hurting. Paid personas keep the plain random-jitter social call.
            bool triage = avatar.holoCore?.ActivePersona == HoloAI_DefOf.HoloAI_Persona_PRISM;

            List<Pawn> colonists = avatar.Map.mapPawns.FreeColonistsSpawned;
            Pawn best = null;
            Pawn saddest = null;
            float saddestMood = float.MaxValue;
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
                float mood = colonist.needs?.mood?.CurLevel ?? float.MaxValue;
                if (triage)
                {
                    if (NearBreak(colonist))
                    {
                        score += 200;
                    }
                    if (mood < saddestMood)
                    {
                        saddestMood = mood;
                        saddest = colonist;
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = colonist;
                }
            }
            // The ship-wide lowest mood beats jitter (but not a near-break bonus
            // already applied above, since the saddest pawn carries both).
            if (triage && saddest != null && saddest != best && bestScore < 200)
            {
                best = saddest;
            }
            return best;
        }

        /// <summary>Below (or within a hair of) the minor mental break threshold.</summary>
        public static bool NearBreak(Pawn colonist)
        {
            float? mood = colonist.needs?.mood?.CurLevel;
            float? threshold = colonist.mindState?.mentalBreaker?.BreakThresholdMinor;
            return mood != null && threshold != null && mood.Value < threshold.Value + 0.05f;
        }
    }
}
