using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// A.T.H.E.N.A.'s signature: impromptu seminars for crew who are idling or
    /// recreating on the ship — never anyone working. The seminar memory doubles as
    /// the per-colonist cooldown (3 in-game hours). XP goes through the vanilla
    /// learning pipeline, so passions and the daily saturation cap all apply.
    /// </summary>
    public class JobGiver_HoloSeminar : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar)
                || avatar.holoCore?.ActivePersona != HoloAI_DefOf.HoloAI_Persona_ATHENA)
            {
                return null;
            }

            foreach (Pawn colonist in avatar.Map.mapPawns.FreeColonistsSpawned)
            {
                if (IsDueStudent(avatar, colonist))
                {
                    Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_Seminar, colonist);
                    job.locomotionUrgency = LocomotionUrgency.Amble;
                    return job;
                }
            }
            return null;
        }

        internal static bool IsDueStudent(Pawn_HoloAvatar avatar, Pawn colonist)
        {
            if (colonist.Dead || colonist.Downed || !colonist.Awake() || colonist.Drafted
                || colonist.InMentalState || colonist.skills == null
                || avatar.Map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true
                || !avatar.CanReach(colonist, PathEndMode.Touch, Danger.None))
            {
                return false;
            }
            // Downtime only — she lectures the idle and the relaxing, never the working.
            bool idle = colonist.mindState.IsIdle || colonist.CurJob?.def.joyKind != null;
            if (!idle || JobDriver_HoloSeminar.PickSkill(colonist) == null)
            {
                return false;
            }
            // The memory IS the cooldown (0.125 days = 3 in-game hours).
            return colonist.needs?.mood?.thoughts?.memories?
                .GetFirstMemoryOfDef(HoloAI_DefOf.HoloAI_AttendedSeminar) == null;
        }
    }
}
