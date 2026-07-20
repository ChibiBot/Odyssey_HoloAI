using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// I.X.I.A.'s prisoner-feeding routine. A hologram can't haul, so instead of
    /// carrying a meal she walks to the hungry prisoner's cell and rematerializes a
    /// food stack — teleporting it from wherever it sits on the ship straight into the
    /// prisoner's room, with the same lightning-glow phase effect used elsewhere.
    /// TargetA = the prisoner; TargetB = the food source to relocate.
    /// </summary>
    public class JobDriver_HoloWardenFood : JobDriver
    {
        private const int MaterializeDurationTicks = 120;

        private Pawn Prisoner => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        private Thing FoodSource => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // A hologram reserves nothing; she teleports the food rather than hauling it,
            // so real haulers are free to touch the same stack until the instant she fires.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnDespawnedOrNull(TargetIndex.B);
            this.FailOnMentalState(TargetIndex.A);
            this.FailOn(() => !StillNeedsFood(Prisoner));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil materialize = ToilMaker.MakeToil("HoloWardenMaterializeFood");
            materialize.defaultCompleteMode = ToilCompleteMode.Delay;
            materialize.defaultDuration = MaterializeDurationTicks;
            materialize.tickAction = () => pawn.rotationTracker.FaceTarget(Prisoner);
            yield return materialize;

            Toil deliver = ToilMaker.MakeToil("HoloWardenDeliverFood");
            deliver.defaultCompleteMode = ToilCompleteMode.Instant;
            deliver.initAction = () =>
            {
                if (FoodSource != null && FoodSource.Spawned && StillNeedsFood(Prisoner))
                {
                    TeleportFood();
                }
            };
            yield return deliver;
        }

        private static bool StillNeedsFood(Pawn prisoner)
        {
            if (prisoner?.guest == null || !prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure)
            {
                return false;
            }
            if (!prisoner.guest.CanBeBroughtFood || prisoner.needs?.food == null)
            {
                return false;
            }
            return prisoner.needs.food.CurLevelPercentage < prisoner.needs.food.PercentageThreshHungry + 0.02f;
        }

        private void TeleportFood()
        {
            Thing food = FoodSource;
            Map map = Prisoner.Map;
            Room room = Prisoner.GetRoom();
            IntVec3 source = food.PositionHeld;

            int count = Mathf.Min(job.count <= 0 ? food.stackCount : job.count, food.stackCount);
            Thing chunk = food.SplitOff(count);

            // Land it on a standable cell inside the prisoner's own room, nearest the
            // prisoner — never leak the meal out through the door.
            bool placed = GenPlace.TryPlaceThing(chunk, Prisoner.Position, map, ThingPlaceMode.Near,
                out Thing result,
                placedAction: null,
                extraValidator: c => c.GetRoom(map) == room && c.Standable(map));
            if (!placed)
            {
                // Fallback: drop it directly at the prisoner's feet rather than lose it.
                GenPlace.TryPlaceThing(chunk, Prisoner.Position, map, ThingPlaceMode.Direct, out result);
            }

            FleckMaker.ThrowLightningGlow(source.ToVector3Shifted(), map, 0.9f);
            if (result != null && result.Spawned)
            {
                FleckMaker.ThrowLightningGlow(result.DrawPos, map, 0.9f);
            }
        }
    }
}
