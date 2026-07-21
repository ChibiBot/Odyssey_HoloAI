using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Hand-feeding for brig charges who cannot feed themselves. Vanilla covers these
    /// with WorkGiver_Warden_Feed / WorkGiver_FeedPatient, whose JobDriver_FoodFeedPatient
    /// is a carry-and-chew toil chain that needs hands; a hologram has none, so she
    /// walks to the patient and rematerializes the meal straight from the ship's
    /// stores into them — Thing.Ingested is the same call FinalizeIngest makes, so
    /// taste thoughts, food poisoning, and drug effects all behave. TargetA = the
    /// patient; TargetB = the food source consumed in place.
    /// </summary>
    public class JobDriver_HoloWardenFeed : JobDriver
    {
        private const int MaterializeDurationTicks = 120;

        private Pawn Patient => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        private Thing FoodSource => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // A hologram reserves nothing; the food is consumed in place, so real
            // haulers are free to touch the same stack until the instant she fires.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOn(() => !StillNeedsFeeding(Patient));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil materialize = ToilMaker.MakeToil("HoloWardenFeedMaterialize");
            materialize.defaultCompleteMode = ToilCompleteMode.Delay;
            materialize.defaultDuration = MaterializeDurationTicks;
            materialize.tickAction = () => pawn.rotationTracker.FaceTarget(Patient);
            yield return materialize;

            Toil feed = ToilMaker.MakeToil("HoloWardenFeedIngest");
            feed.defaultCompleteMode = ToilCompleteMode.Instant;
            feed.initAction = () =>
            {
                if (FoodSource != null && FoodSource.Spawned && StillNeedsFeeding(Patient))
                {
                    MaterializeAndFeed(pawn, Patient, FoodSource);
                }
            };
            yield return feed;
        }

        /// <summary>
        /// Vanilla's ShouldBeFedBySomeone (warden feed + feed patient) both require a
        /// bed — a human warden rescues a downed charge to one first, which a hologram
        /// cannot, so downed-out-of-bed prisoners and slaves qualify too rather than
        /// being left to starve on the floor.
        /// </summary>
        internal static bool StillNeedsFeeding(Pawn patient)
        {
            if (patient?.guest == null)
            {
                return false;
            }
            bool secure = patient.IsPrisonerOfColony
                ? patient.guest.PrisonerIsSecure
                : ModsConfig.IdeologyActive && patient.IsSlaveOfColony && patient.guest.SlaveIsSecure;
            if (!secure)
            {
                return false;
            }
            if (patient.IsPrisonerOfColony && !patient.guest.CanBeBroughtFood)
            {
                return false;
            }
            if (patient.needs?.food == null || !FeedPatientUtility.IsHungry(patient))
            {
                return false;
            }
            return (patient.Downed && !patient.InBed()) || FoodUtility.ShouldBeFedBySomeone(patient);
        }

        /// <summary>
        /// The ingest half of vanilla's FinalizeIngest toil, fired directly: Ingested
        /// consumes the right amount off the stack (or the whole dispensed meal),
        /// handles destruction, and returns the nutrition actually taken in. A paste
        /// dispenser source is operated remotely via TryDispenseFood first.
        /// </summary>
        internal static bool MaterializeAndFeed(Pawn avatar, Pawn patient, Thing foodSource)
        {
            Map map = patient.Map;
            IntVec3 sourceCell = foodSource.PositionHeld;
            Thing meal = foodSource;
            if (foodSource is Building_NutrientPasteDispenser dispenser)
            {
                meal = dispenser.TryDispenseFood();
                if (meal == null)
                {
                    return false;
                }
            }
            float wanted = patient.needs?.food?.NutritionWanted ?? 0f;
            if (wanted <= 0f)
            {
                return false;
            }
            float ingested = meal.Ingested(patient, wanted);
            if (!patient.Dead && patient.needs?.food != null)
            {
                patient.needs.food.CurLevel += ingested;
                patient.records.AddTo(RecordDefOf.NutritionEaten, ingested);
            }
            FleckMaker.ThrowLightningGlow(sourceCell.ToVector3Shifted(), map, 0.9f);
            FleckMaker.ThrowLightningGlow(patient.DrawPos, map, 0.9f);
            return true;
        }
    }
}
