using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// I.X.I.A. runs the brig herself the instant a prisoner or slave is due — no
    /// player click-target step, same auto-firing pattern as JobGiver_HoloChat (this
    /// node sits above it in the think tree so it takes priority). Eligibility mirrors
    /// vanilla's own WorkGiver_Warden_Chat.JobOnThing (recruit attempt / resistance
    /// reduction) and WorkGiver_Warden_SuppressSlave.JobOnThing (slave suppression),
    /// read straight off the target's own guest tracker/need so this stays in lockstep
    /// with any future vanilla tuning. No cooldown of our own — JobDriver_HoloWarden
    /// already re-validates the target every tick (FailOnMentalState/FailOnNotAwake/
    /// IsValidTarget), and the user wants her to answer the instant it's ready.
    /// </summary>
    public class JobGiver_HoloWarden : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_HoloAvatar avatar)
                || avatar.holoCore?.ActivePersona != HoloAI_DefOf.HoloAI_Persona_IXIA)
            {
                return null;
            }

            // A hungry prisoner outranks recruitment/suppression — feed first.
            Job foodJob = TryGiveFoodDeliveryJob(avatar);
            if (foodJob != null)
            {
                return foodJob;
            }

            Pawn target = FindDueTarget(avatar);
            return target == null ? null : JobMaker.MakeJob(HoloAI_DefOf.HoloAI_WardenInteract, target);
        }

        /// <summary>
        /// Mirrors WorkGiver_Warden_DeliverFood.JobOnThing, but instead of a haul job she
        /// gets a teleport job: the food source only needs to sit on the ship's own
        /// substructure (a gravship panel), since she rematerializes it rather than
        /// carrying it. She never leaves the ship to fetch a meal.
        /// </summary>
        private static Job TryGiveFoodDeliveryJob(Pawn_HoloAvatar avatar)
        {
            foreach (Pawn prisoner in avatar.Map.mapPawns.PrisonersOfColonySpawned)
            {
                if (prisoner.guest == null || !prisoner.guest.PrisonerIsSecure
                    || prisoner.InAggroMentalState || prisoner.IsForbidden(avatar)
                    || prisoner.IsFormingCaravan()
                    || !avatar.CanReach(prisoner, PathEndMode.Touch, Danger.None))
                {
                    continue;
                }
                if (!prisoner.guest.CanBeBroughtFood || !prisoner.Position.IsInPrisonCell(prisoner.Map)
                    || prisoner.needs?.food == null)
                {
                    continue;
                }
                if (prisoner.needs.food.CurLevelPercentage >= prisoner.needs.food.PercentageThreshHungry + 0.02f
                    || WardenFeedUtility.ShouldBeFed(prisoner) || RoomAlreadyStocked(prisoner))
                {
                    continue;
                }
                if (!FoodUtility.TryFindBestFoodSourceFor(avatar, prisoner,
                        prisoner.needs.food.CurCategory == HungerCategory.Starving,
                        out Thing foodSource, out ThingDef foodDef,
                        canRefillDispenser: false, canUseInventory: false, canUsePackAnimalInventory: false,
                        allowForbidden: false, allowCorpse: false, allowSociallyImproper: false,
                        allowHarvest: false, forceScanWholeMap: false, ignoreReservations: true,
                        calculateWantedStackCount: true))
                {
                    continue;
                }
                if (foodSource.GetRoom() == prisoner.GetRoom()
                    || avatar.Map.terrainGrid.FoundationAt(foodSource.PositionHeld)?.IsSubstructure != true)
                {
                    continue;
                }
                float nutrition = FoodUtility.GetNutrition(prisoner, foodSource, foodDef);
                Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_WardenDeliverFood, prisoner, foodSource);
                job.count = FoodUtility.WillIngestStackCountOf(prisoner, foodDef, nutrition);
                return job;
            }
            return null;
        }

        /// <summary>True if the prisoner's own cell already holds food it will eat —
        /// keeps her from teleporting the whole larder in one meal at a time.</summary>
        private static bool RoomAlreadyStocked(Pawn prisoner)
        {
            Room room = prisoner.GetRoom();
            if (room == null)
            {
                return false;
            }
            foreach (Region region in room.Regions)
            {
                foreach (Thing thing in region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                {
                    if (thing.def.IsIngestible && prisoner.WillEat(thing))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static Pawn FindDueTarget(Pawn_HoloAvatar avatar)
        {
            foreach (Pawn candidate in avatar.Map.mapPawns.SlavesAndPrisonersOfColonySpawned)
            {
                if (IsDueSlave(avatar, candidate) || IsDuePrisoner(avatar, candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Mirrors WorkGiver_Warden.ShouldTakeCareOfSlave + WorkGiver_Warden_SuppressSlave.JobOnThing.</summary>
        private static bool IsDueSlave(Pawn_HoloAvatar avatar, Pawn candidate)
        {
            if (!ModsConfig.IdeologyActive || !candidate.IsSlaveOfColony || candidate.guest == null)
            {
                return false;
            }
            if (!candidate.guest.SlaveIsSecure || !candidate.Spawned || candidate.InAggroMentalState
                || candidate.IsForbidden(avatar) || candidate.IsFormingCaravan())
            {
                return false;
            }
            if (candidate.guest.slaveInteractionMode != SlaveInteractionModeDefOf.Suppress
                || candidate.Downed || !candidate.Awake())
            {
                return false;
            }
            if (!candidate.needs.TryGetNeed(out Need_Suppression need) || !need.CanBeSuppressedNow
                || !candidate.guest.ScheduledForSlaveSuppression)
            {
                return false;
            }
            return avatar.CanReach(candidate, PathEndMode.Touch, Danger.None);
        }

        /// <summary>Mirrors WorkGiver_Warden.ShouldTakeCareOfPrisoner + WorkGiver_Warden_Chat.JobOnThing.</summary>
        private static bool IsDuePrisoner(Pawn_HoloAvatar avatar, Pawn candidate)
        {
            if (!candidate.IsPrisonerOfColony || candidate.guest == null || !candidate.guest.PrisonerIsSecure
                || !candidate.Spawned || candidate.InAggroMentalState || candidate.IsForbidden(avatar)
                || candidate.IsFormingCaravan())
            {
                return false;
            }
            PrisonerInteractionModeDef mode = candidate.guest.ExclusiveInteractionMode;
            if ((mode != PrisonerInteractionModeDefOf.AttemptRecruit && mode != PrisonerInteractionModeDefOf.ReduceResistance)
                || !candidate.guest.ScheduledForInteraction)
            {
                return false;
            }
            if (mode == PrisonerInteractionModeDefOf.ReduceResistance && candidate.guest.Resistance <= 0f)
            {
                return false;
            }
            if ((candidate.Downed && !candidate.InBed()) || !candidate.Awake())
            {
                return false;
            }
            return avatar.CanReach(candidate, PathEndMode.Touch, Danger.None);
        }
    }
}
