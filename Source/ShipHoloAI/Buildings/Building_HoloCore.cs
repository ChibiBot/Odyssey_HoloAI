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
        private HoloPersonaDef activePersona;

        private CompPowerTrader powerComp;

        private const int StateCheckInterval = 60;

        public Building_HoloCore()
        {
            innerContainer = new ThingOwner<Pawn>(this);
        }

        public bool Powered => powerComp == null || powerComp.PowerOn;

        public Pawn_HoloAvatar Avatar => avatar;

        public HoloPersonaDef ActivePersona => activePersona ?? HoloAI_DefOf.HoloAI_Persona_PRISM;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        protected override void Tick()
        {
            base.Tick();
            // MinifiedThing forwards ticks to its inner building — while minified
            // (or otherwise unspawned) there is no Map to project onto.
            if (!Spawned || Map == null)
            {
                return;
            }
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
            if (!Spawned || Map == null || !TryFindProjectionCell(out IntVec3 cell))
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
            avatar.holoCore = this;
            avatar.ApplyPersonaStyle(ActivePersona);
        }

        /// <summary>
        /// Swap the resident persona for the one held in a matrix item. The current
        /// persona's matrix (if it has one — P.R.I.S.M. does not) is ejected beside
        /// the core, the old avatar dissolves, and the newcomer projects on the next
        /// state check.
        /// </summary>
        public void InstallPersona(Thing matrixItem)
        {
            HoloPersonaDef newPersona = matrixItem?.def
                .GetModExtension<HoloPersonaMatrixExtension>()?.persona;
            if (newPersona == null || newPersona == ActivePersona)
            {
                return;
            }

            ThingDef ejectedDef = ActivePersona.matrixItem;

            StoreAvatar();
            innerContainer.ClearAndDestroyContents();
            avatar = null;

            activePersona = newPersona;
            matrixItem.Destroy();
            if (ejectedDef != null)
            {
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(ejectedDef), Position, Map, ThingPlaceMode.Near);
            }
            FleckMaker.ThrowLightningGlow(DrawPos, Map, 1.4f);
            Messages.Message("HoloAI_PersonaInstalled".Translate(newPersona.label), this,
                MessageTypeDefOf.PositiveEvent);
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

            yield return new Command_Action
            {
                defaultLabel = "HoloAI_InstallPersonaGizmo".Translate(),
                defaultDesc = "HoloAI_InstallPersonaGizmoDesc".Translate(ActivePersona.label),
                icon = def.uiIcon,
                action = OpenInstallMenu,
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

        private void OpenInstallMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Thing thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                Thing matrix = thing;
                if (matrix.def.GetModExtension<HoloPersonaMatrixExtension>() == null
                    || matrix.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }
                options.Add(new FloatMenuOption(matrix.LabelCap, () => OrderInstall(matrix)));
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption((string)"HoloAI_NoMatrices".Translate(), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OrderInstall(Thing matrix)
        {
            Pawn worker = null;
            float bestDist = float.MaxValue;
            foreach (Pawn colonist in Map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist.Downed || colonist.Drafted || colonist.InMentalState
                    || !colonist.CanReserveAndReach(matrix, PathEndMode.ClosestTouch, Danger.Some))
                {
                    continue;
                }
                float dist = colonist.Position.DistanceToSquared(matrix.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    worker = colonist;
                }
            }
            if (worker == null)
            {
                Messages.Message("HoloAI_NoInstaller".Translate(), this, MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }
            Job job = JobMaker.MakeJob(HoloAI_DefOf.HoloAI_InstallPersona, matrix, this);
            job.count = 1;
            worker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            string state = AvatarSpawned
                ? (string)"HoloAI_StateProjected".Translate()
                : (string)"HoloAI_StateDormant".Translate();
            string persona = "HoloAI_ActivePersona".Translate(ActivePersona.label);
            string combined = persona + "\n" + state;
            return text.NullOrEmpty() ? combined : text + "\n" + combined;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref avatar, "avatar");
            Scribe_Values.Look(ref projectionEnabled, "projectionEnabled", defaultValue: true);
            Scribe_Defs.Look(ref activePersona, "activePersona");
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
