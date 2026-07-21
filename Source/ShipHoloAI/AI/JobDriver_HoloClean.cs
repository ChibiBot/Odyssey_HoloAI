using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Zip to the filth, scrub for a moment scaled by its thickness (~colonist-parity
    /// cleaning speed: vanilla is ~70 ticks of work per thickness), then it dissolves
    /// in a flicker of light. Occasionally she has opinions about the mess.
    /// </summary>
    public class JobDriver_HoloClean : JobDriver
    {
        private const int TicksPerThickness = 60;
        private const float RemarkChance = 0.15f;

        private Filth Filth => (Filth)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Nothing physical is claimed; a hologram reserves nothing — a colonist
            // beating her to the same filth just fails this job harmlessly.
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil scrub = ToilMaker.MakeToil("HoloScrub");
            scrub.defaultCompleteMode = ToilCompleteMode.Delay;
            scrub.defaultDuration = TicksPerThickness;
            scrub.initAction = () =>
            {
                // Thicker filth takes proportionally longer.
                ticksLeftThisToil = TicksPerThickness * Mathf.Max(1, Filth.thickness);
            };
            scrub.tickAction = () => pawn.rotationTracker.FaceTarget(Filth);
            yield return scrub;

            Toil fire = ToilMaker.MakeToil("HoloScrubFire");
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            fire.initAction = () =>
            {
                if (!Filth.Destroyed)
                {
                    FireClean(pawn, Filth);
                }
            };
            yield return fire;
        }

        /// <summary>Static so the self-test can assert the effect directly.</summary>
        internal static void FireClean(Pawn avatar, Filth filth)
        {
            FleckMaker.ThrowLightningGlow(filth.DrawPos, filth.Map, 0.3f);
            filth.Destroy();
            if (Rand.Chance(RemarkChance))
            {
                PrismSpeech.Bark(avatar, "hermes_clean");
            }
        }
    }
}
