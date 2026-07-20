using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Standalone, opt-in scenario for hands-on verification of I.X.I.A.'s automatic
    /// warden behavior against real physical rooms — deliberately separate from
    /// HoloAISelfTest so it never runs as part of the normal QA gate. Inert unless
    /// HOLOAI_IXIA_ROOMTEST=1. Builds a walled 5x5 prisoner room (one door, one
    /// prisoner bed) and a walled 5x5 slave room (one door, one bed), installs
    /// I.X.I.A., and logs [HoloAI IXIA-RoomTest] lines tracking whether she
    /// autonomously walks over and fires the recruit-attempt / suppression
    /// interactions with no player input.
    /// </summary>
    public class HoloAI_IxiaRoomTest : GameComponent
    {
        private static readonly bool Enabled =
            Environment.GetEnvironmentVariable("HOLOAI_IXIA_ROOMTEST") == "1";

        private const int SetupTick = 300;
        private const int MaxObserveTicks = 8000;

        private int ticks;
        private bool done;
        private Building_HoloCore core;
        private Pawn prisoner;
        private Pawn slave;
        private float prisonerResistanceAtStart;
        private bool recruitProgressObserved;
        private bool suppressProgressObserved;
        private bool avatarEverSpawned;

        public HoloAI_IxiaRoomTest(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (!Enabled || done)
            {
                return;
            }
            ticks++;

            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }

            if (ticks == SetupTick)
            {
                Setup(map);
                return;
            }
            if (core == null)
            {
                return;
            }

            if (ticks % 250 == 0)
            {
                LogProgress();
            }
            if (ticks - SetupTick >= MaxObserveTicks)
            {
                Finish(timedOut: true);
            }
        }

        private void Setup(Map map)
        {
            IntVec3 center = map.Center;
            TerrainDef substructure = DefDatabase<TerrainDef>.GetNamed("Substructure");
            foreach (IntVec3 c in CellRect.CenteredOn(center, 16))
            {
                if (!c.InBounds(map))
                {
                    continue;
                }
                map.terrainGrid.SetFoundation(c, substructure);
                foreach (Thing t in c.GetThingList(map).ListFullCopy())
                {
                    if (t.def.destroyable && !(t is Pawn))
                    {
                        t.Destroy();
                    }
                }
            }

            Thing engine = ThingMaker.MakeThing(ThingDef.Named("GravEngine"));
            engine.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(engine, center + new IntVec3(-8, 0, -8), map);

            Thing coreThing = ThingMaker.MakeThing(HoloAI_DefOf.HoloAI_HoloCore);
            coreThing.SetFaction(Faction.OfPlayer);
            core = (Building_HoloCore)GenSpawn.Spawn(coreThing, center, map);
            ForcePower(true);

            // Otherwise whichever quicktest-scenario colonist has Warden work enabled
            // races IXIA to the same prisoner/slave and the result is a false pass —
            // this test is specifically about HER doing it, unassisted.
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                colonist.workSettings?.Disable(WorkTypeDefOf.Warden);
            }

            IntVec3 prisonerRoomCenter = center + new IntVec3(9, 0, 5);
            IntVec3 prisonerDoor = BuildEnclosedRoom(map, prisonerRoomCenter, doorOnWest: true);
            Thing prisonerBedThing = ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            prisonerBedThing.SetFaction(Faction.OfPlayer);
            Building_Bed prisonerBed = (Building_Bed)GenSpawn.Spawn(prisonerBedThing, prisonerRoomCenter, map);
            prisonerBed.ForPrisoners = true;

            IntVec3 slaveRoomCenter = center + new IntVec3(9, 0, -5);
            IntVec3 slaveDoor = BuildEnclosedRoom(map, slaveRoomCenter, doorOnWest: true);
            Thing slaveBedThing = ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            slaveBedThing.SetFaction(Faction.OfPlayer);
            Building_Bed slaveBed = (Building_Bed)GenSpawn.Spawn(slaveBedThing, slaveRoomCenter, map);
            slaveBed.ForOwnerType = BedOwnerType.Slave;

            Faction hostileFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            prisoner = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, hostileFaction);
            GenSpawn.Spawn(prisoner, prisonerRoomCenter + new IntVec3(1, 0, 0), map);
            prisoner.guest.CapturedBy(Faction.OfPlayer);
            prisoner.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.AttemptRecruit);
            prisonerResistanceAtStart = prisoner.guest.Resistance;

            slave = PawnGenerator.GeneratePawn(PawnKindDefOf.Villager, hostileFaction);
            GenSpawn.Spawn(slave, slaveRoomCenter + new IntVec3(1, 0, 0), map);
            slave.guest.CapturedBy(Faction.OfPlayer);
            slave.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);

            ThingDef ixiaMatrixDef = DefDatabase<ThingDef>.GetNamedSilentFail("HoloAI_Matrix_IXIA");
            if (ixiaMatrixDef == null)
            {
                Log.Error("[HoloAI IXIA-RoomTest] HoloAI_Matrix_IXIA def not found; aborting");
                Finish(timedOut: false);
                return;
            }
            Thing ixiaMatrix = GenSpawn.Spawn(ThingMaker.MakeThing(ixiaMatrixDef), center + new IntVec3(-3, 0, 0), map);
            core.InstallPersona(ixiaMatrix);

            Find.CameraDriver?.JumpToCurrentMapLoc(center);
            Log.Message("[HoloAI IXIA-RoomTest] setup complete. core=" + center
                + " prisonerRoom=" + prisonerRoomCenter + " (door " + prisonerDoor + ")"
                + " slaveRoom=" + slaveRoomCenter + " (door " + slaveDoor + ")"
                + " prisoner=" + prisoner.ThingID + " slave=" + slave.ThingID
                + " startingResistance=" + prisonerResistanceAtStart);
        }

        /// <summary>Walls a 5x5 interior with a single door on one side (Bed/Wall/Door
        /// all built from wood/steel so the test never depends on a research state).</summary>
        private static IntVec3 BuildEnclosedRoom(Map map, IntVec3 roomCenter, bool doorOnWest)
        {
            CellRect interior = CellRect.CenteredOn(roomCenter, 2); // 5x5
            CellRect footprint = interior.ExpandedBy(1); // 7x7 including the wall ring
            IntVec3 doorCell = doorOnWest
                ? new IntVec3(footprint.minX, roomCenter.y, roomCenter.z)
                : new IntVec3(footprint.maxX, roomCenter.y, roomCenter.z);

            foreach (IntVec3 c in footprint.Cells)
            {
                if (!c.InBounds(map) || interior.Contains(c))
                {
                    continue;
                }
                foreach (Thing t in c.GetThingList(map).ListFullCopy())
                {
                    if (t.def.destroyable)
                    {
                        t.Destroy();
                    }
                }
                ThingDef buildingDef = c == doorCell ? ThingDefOf.Door : ThingDefOf.Wall;
                Thing building = ThingMaker.MakeThing(buildingDef, ThingDefOf.Steel);
                building.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(building, c, map);
            }
            return doorCell;
        }

        private void LogProgress()
        {
            Pawn_HoloAvatar avatar = core.Avatar;
            string personaName = core.ActivePersona?.defName;
            string avatarState = avatar == null ? "null"
                : (avatar.Spawned ? "spawned@" + avatar.Position : "stored");
            string avatarJob = avatar?.CurJob?.def?.defName ?? "-";
            if (avatar != null && avatar.Spawned)
            {
                avatarEverSpawned = true;
            }

            if (prisoner != null && !prisoner.Dead)
            {
                float resistanceNow = prisoner.guest.Resistance;
                if (!recruitProgressObserved && resistanceNow < prisonerResistanceAtStart)
                {
                    recruitProgressObserved = true;
                    string verdict = avatarEverSpawned ? "PASS" : "SUSPECT (avatar never spawned - attribute to a colonist, not I.X.I.A.)";
                    Log.Message("[HoloAI IXIA-RoomTest] " + verdict + ": prisoner resistance dropped ("
                        + prisonerResistanceAtStart + " -> " + resistanceNow + ") at tick " + ticks);
                }
                if (prisoner.guest.Resistance <= 0f && !prisoner.IsPrisonerOfColony)
                {
                    Log.Message("[HoloAI IXIA-RoomTest] PASS: prisoner fully recruited at tick " + ticks);
                }
            }
            if (slave != null && !slave.Dead)
            {
                slave.needs.TryGetNeed(out Need_Suppression suppressionNeed);
                if (!suppressProgressObserved && slave.mindState.lastSlaveSuppressedTick > 0)
                {
                    suppressProgressObserved = true;
                    string verdict = avatarEverSpawned ? "PASS" : "SUSPECT (avatar never spawned - attribute to a colonist, not I.X.I.A.)";
                    Log.Message("[HoloAI IXIA-RoomTest] " + verdict + ": slave suppression interaction fired at tick "
                        + ticks + " (lastSlaveSuppressedTick=" + slave.mindState.lastSlaveSuppressedTick
                        + ", suppression need=" + suppressionNeed?.CurLevel + ")");
                }
            }

            Log.Message("[HoloAI IXIA-RoomTest] tick " + ticks + " persona=" + personaName
                + " avatar=" + avatarState + " job=" + avatarJob
                + " prisonerResistance=" + prisoner?.guest?.Resistance
                + " slaveLastSuppressed=" + slave?.mindState?.lastSlaveSuppressedTick);

            if (recruitProgressObserved && suppressProgressObserved)
            {
                Finish(timedOut: false);
            }
        }

        private void ForcePower(bool on)
        {
            CompPowerTrader comp = core?.GetComp<CompPowerTrader>();
            if (comp != null && comp.PowerOn != on)
            {
                comp.PowerOn = on;
            }
        }

        private void Finish(bool timedOut)
        {
            done = true;
            Log.Message("[HoloAI IXIA-RoomTest] COMPLETE. timedOut=" + timedOut
                + " recruitProgressObserved=" + recruitProgressObserved
                + " suppressProgressObserved=" + suppressProgressObserved);
        }
    }
}
