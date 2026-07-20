using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Hurry to the patient, project a stabilization field for a few seconds, then
    /// tend without medicine (TendUtility.DoTend is verified safe from a skill-less
    /// ToolUser). The delay's fail conditions include a real doctor reserving the
    /// patient — she abandons the attempt the moment actual medicine arrives.
    /// </summary>
    public class JobDriver_HoloMedic : JobDriver
    {
        private const int StabilizeDurationTicks = 420;

        private Pawn Patient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves no one — deliberately,
            // so a real doctor can still claim the patient out from under her.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Patient.Dead
                || !HealthAIUtility.ShouldBeTendedNowByPlayer(Patient)
                || Map.reservationManager.FirstRespectedReserver(Patient, pawn) != null);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil stabilize = ToilMaker.MakeToil("HoloStabilize");
            stabilize.defaultCompleteMode = ToilCompleteMode.Delay;
            stabilize.defaultDuration = StabilizeDurationTicks;
            stabilize.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(Patient);
                if (pawn.IsHashIntervalTick(120))
                {
                    FleckMaker.ThrowLightningGlow(Patient.DrawPos, Map, 0.3f);
                }
            };
            yield return stabilize;

            Toil fire = ToilMaker.MakeToil("HoloStabilizeFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (Patient.Spawned && !Patient.Dead)
                {
                    FireEmergencyTend(pawn, Patient);
                }
            };
            yield return fire;
        }

        /// <summary>Static so the self-test can assert the effect directly.</summary>
        internal static void FireEmergencyTend(Pawn avatar, Pawn patient)
        {
            TendUtility.DoTend(avatar, patient, null);
            FleckMaker.ThrowLightningGlow(patient.DrawPos, patient.Map, 0.6f);
            PrismSpeech.Say(patient.Map, "aceso_emergency");
            if (avatar is Pawn_HoloAvatar holo)
            {
                holo.nextEmergencyTendTick =
                    Find.TickManager.TicksGame + JobGiver_HoloMedic.EmergencyCooldownTicks;
            }
        }
    }
}
