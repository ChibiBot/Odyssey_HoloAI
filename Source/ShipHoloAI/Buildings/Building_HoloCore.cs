using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// The holocore projects its resident persona as a roaming hologram while powered.
    /// The avatar pawn is ALWAYS either spawned on the map or held in this building's
    /// inner container — despawning the core (including gravship launch) stores her, so
    /// she travels inside the core and survives save/load and flight.
    /// </summary>
    public class Building_HoloCore : Building, IThingHolder
    {
        private ThingOwner<Pawn> innerContainer;
        private Pawn_HoloAvatar avatar;
        private bool projectionEnabled = true;

        private CompPowerTrader powerComp;

        private const int StateCheckInterval = 60;

        public Building_HoloCore()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }

        public bool Powered => powerComp == null || powerComp.PowerOn;

        public Pawn_HoloAvatar Avatar => avatar;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(StateCheckInterval))
            {
                bool wantProjected = Powered && projectionEnabled;
                if (wantProjected && AvatarStored)
                {
                    ProjectAvatar();
                }
                else if (!wantProjected && AvatarSpawned)
                {
                    StoreAvatar();
                }
            }
        }

        private bool AvatarSpawned => avatar != null && avatar.Spawned;

        private bool AvatarStored => !AvatarSpawned;

        private void ProjectAvatar()
        {
            if (!TryFindProjectionCell(out IntVec3 cell))
            {
                return;
            }
            if (avatar == null)
            {
                GeneratePersona();
            }
            innerContainer.Remove(avatar);
            GenSpawn.Spawn(avatar, cell, Map);
            FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), Map, 1.2f);
        }

        public void StoreAvatar()
        {
            if (!AvatarSpawned)
            {
                return;
            }
            FleckMaker.ThrowLightningGlow(avatar.DrawPos, avatar.Map, 0.8f);
            avatar.jobs?.StopAll();
            avatar.DeSpawn();
            if (!innerContainer.Contains(avatar))
            {
                innerContainer.TryAdd(avatar, canMergeWithExistingStacks: false);
            }
        }

        private void GeneratePersona()
        {
            Pawn pawn = PawnGenerator.GeneratePawn(HoloAI_DefOf.HoloAI_PRISM, Faction.OfPlayer);
            avatar = (Pawn_HoloAvatar)pawn;
            avatar.gender = Gender.Female;
            avatar.Name = new NameSingle("P.R.I.S.M.");
            avatar.holoCore = this;
        }

        private bool TryFindProjectionCell(out IntVec3 result)
        {
            return CellFinder.TryFindRandomCellNear(Position, Map, 2,
                c => c.Standable(Map) && !c.Fogged(Map), out result);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            StoreAvatar();
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            StoreAvatar();
            base.Destroy(mode);
            if (mode != DestroyMode.Vanish)
            {
                // The core is gone and its persona with it.
                innerContainer.ClearAndDestroyContents();
                avatar = null;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "HoloAI_ToggleProjection".Translate(),
                defaultDesc = "HoloAI_ToggleProjectionDesc".Translate(),
                icon = def.uiIcon,
                isActive = () => projectionEnabled,
                toggleAction = () => projectionEnabled = !projectionEnabled,
            };

            if (AvatarSpawned)
            {
                yield return new Command_Target
                {
                    defaultLabel = "HoloAI_Summon".Translate(),
                    defaultDesc = "HoloAI_SummonDesc".Translate(),
                    icon = def.uiIcon,
                    targetingParams = new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetPawns = false,
                        canTargetBuildings = false,
                    },
                    action = target =>
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, target.Cell);
                        avatar.jobs.StartJob(job, JobCondition.InterruptForced);
                    },
                };
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string state = AvatarSpawned
                ? (string)"HoloAI_StateProjected".Translate()
                : (string)"HoloAI_StateDormant".Translate();
            return text.NullOrEmpty() ? state : text + "\n" + state;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref avatar, "avatar");
            Scribe_Values.Look(ref projectionEnabled, "projectionEnabled", defaultValue: true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Pawn>(this);
            }
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }
    }
}
