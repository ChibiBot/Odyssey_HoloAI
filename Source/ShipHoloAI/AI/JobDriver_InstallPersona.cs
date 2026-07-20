using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>Carry a persona matrix to the holocore and install it.</summary>
    public class JobDriver_InstallPersona : JobDriver
    {
        private const int InstallTicks = 240;

        private Thing Matrix => job.GetTarget(TargetIndex.A).Thing;

        private Building_HoloCore Core => (Building_HoloCore)job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Matrix, job, 1, 1, null, errorOnFailed)
                && pawn.Reserve(Core, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnForbidden(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.B, InstallTicks, useProgressBar: true)
                .FailOnDestroyedOrNull(TargetIndex.B);

            Toil install = ToilMaker.MakeToil("InstallPersona");
            install.initAction = () =>
            {
                if (pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out Thing dropped))
                {
                    Core.InstallPersona(dropped);
                }
            };
            install.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return install;
        }
    }
}
