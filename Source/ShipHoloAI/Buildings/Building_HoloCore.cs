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
        // The persona archive: installing a matrix consumes it (research-disc
        // style) and permanently unlocks its persona here; switching among
        // archived personas afterward is instant and free.
        private List<HoloPersonaDef> archivedPersonas = new List<HoloPersonaDef>();

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

        /// <summary>True when this core can project the persona without a matrix:
        /// built-in (P.R.I.S.M. has no matrix item), currently resident, or already
        /// installed into the archive.</summary>
        public bool IsUnlocked(HoloPersonaDef persona)
        {
            return persona != null
                && (persona.matrixItem == null
                    || persona == ActivePersona
                    || archivedPersonas.Contains(persona));
        }

        /// <summary>
        /// Install a persona matrix, research-disc style: the matrix is consumed and
        /// its persona joins this core's permanent archive, then takes over the
        /// projection. Installing a matrix whose persona is already archived wastes
        /// nothing — the matrix is left alone and the persona simply activates.
        /// </summary>
        public void InstallPersona(Thing matrixItem)
        {
            HoloPersonaDef newPersona = matrixItem?.def
                .GetModExtension<HoloPersonaMatrixExtension>()?.persona;
            if (newPersona == null)
            {
                return;
            }
            if (IsUnlocked(newPersona))
            {
                ActivatePersona(newPersona);
                return;
            }
            matrixItem.Destroy();
            archivedPersonas.Add(newPersona);
            SwapPersona(newPersona);
        }

        /// <summary>Switch the projection to an archived (or built-in) persona — no
        /// matrix, no hauling job; the archive's whole point. No-op if the persona
        /// is locked or already resident.</summary>
        public void ActivatePersona(HoloPersonaDef persona)
        {
            if (persona == null || persona == ActivePersona || !IsUnlocked(persona))
            {
                return;
            }
            SwapPersona(persona);
        }

        /// <summary>Revert to the built-in P.R.I.S.M. persona.</summary>
        public void RestoreDefaultPersona()
        {
            ActivatePersona(HoloAI_DefOf.HoloAI_Persona_PRISM);
        }

        private void SwapPersona(HoloPersonaDef newPersona)
        {
            StoreAvatar();
            innerContainer.ClearAndDestroyContents();
            avatar = null;

            activePersona = newPersona;
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
            Find.WindowStack.Add(new Dialog_PersonaSwitcher(this));
        }

        /// <summary>Every matrix item on the map that could be installed here right now.</summary>
        public IEnumerable<Thing> GetInstallableMatrices()
        {
            if (Map == null)
            {
                yield break;
            }
            foreach (Thing thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
            {
                if (thing.def.GetModExtension<HoloPersonaMatrixExtension>() == null
                    || thing.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }
                yield return thing;
            }
        }

        public void OrderInstall(Thing matrix)
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
            Scribe_Collections.Look(ref archivedPersonas, "archivedPersonas", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null)
                {
                    innerContainer = new ThingOwner<Pawn>(this);
                }
                if (archivedPersonas == null)
                {
                    archivedPersonas = new List<HoloPersonaDef>();
                }
                // Pre-archive saves: whoever is resident was installed from a matrix
                // that no longer exists — grandfather her into the archive.
                if (activePersona?.matrixItem != null && !archivedPersonas.Contains(activePersona))
                {
                    archivedPersonas.Add(activePersona);
                }
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
