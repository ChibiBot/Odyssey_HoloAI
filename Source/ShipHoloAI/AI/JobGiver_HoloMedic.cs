using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// A.C.E.S.O.'s signature: emergency stabilization. When a colonist on the ship
    /// is downed or bleeding, needs tending, and no doctor has claimed them, she
    /// responds herself — at herbal-medicine-grade quality (a conjured photonic dose,
    /// see JobDriver_HoloMedic.FireEmergencyTend) and with no cooldown: an emergency
    /// is an emergency, and she answers every one. Still strictly worse than a real
    /// doctor with industrial medicine, and she yields the instant one claims the
    /// patient.
    /// </summary>
    public class JobGiver_HoloMedic : ThinkNode_JobGiver
    {
        private const float MinBleedRate = 0.1f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar)
                || avatar.holoCore?.ActivePersona != HoloAI_DefOf.HoloAI_Persona_ACESO)
            {
                return null;
            }

            foreach (Pawn colonist in avatar.Map.mapPawns.FreeColonistsSpawned)
            {
                if (IsEmergency(avatar, colonist))
                {
                    return JobMaker.MakeJob(HoloAI_DefOf.HoloAI_EmergencyTend, colonist);
                }
            }
            return null;
        }

        internal static bool IsEmergency(Pawn_HoloAvatar avatar, Pawn colonist)
        {
            if (colonist.Dead || colonist == avatar
                || (!colonist.Downed && colonist.health.hediffSet.BleedRateTotal <= MinBleedRate))
            {
                return false;
            }
            if (!HealthAIUtility.ShouldBeTendedNowByPlayer(colonist)
                || avatar.Map.terrainGrid.FoundationAt(colonist.Position)?.IsSubstructure != true
                || !avatar.CanReach(colonist, PathEndMode.Touch, Danger.None))
            {
                return false;
            }
            // She always yields to flesh-and-blood medicine: anyone reserving the
            // patient (a doctor en route or mid-tend) means this is not her call.
            return avatar.Map.reservationManager.FirstRespectedReserver(colonist, avatar) == null;
        }
    }
}
