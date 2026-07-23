using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace ShipHoloAI
{
    /// <summary>
    /// Headless verification harness. Inert unless the HOLOAI_SELFTEST env var is "1"
    /// (set by the dev workflow, never by players). Builds a substructure patch, spawns
    /// a powered holocore, and asserts the avatar lifecycle, logging [HoloAI SelfTest]
    /// lines that the QA workflow greps for.
    /// </summary>
    public class HoloAISelfTest : GameComponent
    {
        private static readonly bool Enabled = Environment.GetEnvironmentVariable("HOLOAI_SELFTEST") == "1";

        private int ticks;
        private int failures;
        private IntVec3 siteCenter;
        private Building_HoloCore core;
        private Pawn testColonist;
        private bool chatMemoryObserved;
        private bool chatLogObserved;
        private bool done;
        private Thing jobInstallMatrix;
        private Pawn wardenTestPrisoner;
        private Pawn wardenTestSlave;
        private Filth testFilth;
        private HairDef restyledPrismHair;

        public HoloAISelfTest(Game game)
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

            // An exception inside a checkpoint (our bug or another mod's — pawn
            // generation is a favorite) must not silently strand the run without
            // its COMPLETE line: count it as a failure and keep going.
            try
            {
                RunCheckpoint(map);
            }
            catch (Exception e)
            {
                failures++;
                Log.Message("[HoloAI SelfTest] FAIL: checkpoint at tick " + ticks + " threw: " + e);
            }
            if (ticks == 13800 && !done)
            {
                Finish();
            }

            if (ticks > 4200 && ticks % 250 == 0 && !done)
            {
                ObserveChat();
            }

            // Keep the unconnected power comp switched on between checkpoints.
            if (ticks > 1100 && ticks < 1500 || ticks > 2100 && ticks < 2700 || ticks > 3600)
            {
                ForcePower(true);
            }
        }

        private void RunCheckpoint(Map map)
        {
            switch (ticks)
            {
                case 300:
                    Setup(map);
                    break;
                case 1200:
                    ForcePower(true);
                    Check("avatar projected while powered", core.Avatar != null && core.Avatar.Spawned);
                    break;
                case 1400:
                    if (core.Avatar != null && core.Avatar.Spawned)
                    {
                        Check("render tree resolved", core.Avatar.Drawer.renderer.renderTree.Resolved);
                        HairDef before = core.Avatar.CurrentHairDef;
                        Check("default hairstyle resolved", before != null);
                        core.Avatar.CycleHairstyle();
                        Check("hairstyle cycles", core.Avatar.CurrentHairDef != before);
                        // Remembered for the restyle-persistence check at restore time.
                        restyledPrismHair = core.Avatar.CurrentHairDef;
                        Log.Message("[HoloAI SelfTest] hair: " + before?.defName + " -> "
                            + core.Avatar.CurrentHairDef?.defName);
                    }
                    else
                    {
                        Check("render tree resolved", false);
                    }
                    break;
                case 1500:
                    Check("avatar named P.R.I.S.M.", core.Avatar != null && core.Avatar.Name?.ToStringFull == "P.R.I.S.M.");
                    Check("avatar wanders on substructure",
                        core.Avatar != null && core.Avatar.Spawned &&
                        map.terrainGrid.FoundationAt(core.Avatar.Position)?.IsSubstructure == true);
                    ForcePower(false);
                    break;
                case 2100:
                    Check("avatar stored on power loss", core.Avatar == null || !core.Avatar.Spawned);
                    ForcePower(true);
                    break;
                case 2700:
                    Check("avatar re-projected on power restore", core.Avatar != null && core.Avatar.Spawned);
                    Check("avatar identity preserved", core.Avatar != null && core.Avatar.Name?.ToStringFull == "P.R.I.S.M.");
                    core.DeSpawn();
                    break;
                case 2760:
                    Check("avatar travels inside despawned core",
                        core.Avatar != null && !core.Avatar.Spawned &&
                        core.GetDirectlyHeldThings().Contains(core.Avatar));
                    GenSpawn.Spawn(core, siteCenter + new IntVec3(4, 0, 4), map);
                    break;
                case 3600:
                    ForcePower(true);
                    break;
                case 4200:
                    Check("avatar re-projected after core respawn", core.Avatar != null && core.Avatar.Spawned);
                    break;
                case 9000:
                    Check("colonist gained chat memory", chatMemoryObserved);
                    Check("interaction play-log entry recorded and resolvable", chatLogObserved);
                    break;
                case 9100:
                    Check("low-fuel line resolves from grammar",
                        !PrismSpeech.ResolveLine("announce_lowfuel").NullOrEmpty());
                    Check("persona speech keyword coverage", PersonaSpeechCoverage());
                    Find.LetterStack.ReceiveLetter("self-test threat", "letter sent by the HoloAI self-test",
                        LetterDefOf.ThreatSmall, new LookTargets(core));
                    break;
                case 9300:
                    TestUnattackable();
                    TestStylingTrackers();
                    StartTeleportTest(map);
                    SpawnMinifiedCore(map);
                    break;
                case 9800:
                    // On-substructure is the ship-bound invariant; no distance clause —
                    // she lands within 3 cells of the core but has ~200 ticks to wander
                    // anywhere on the patch before this fires.
                    Check("avatar teleported back to substructure",
                        core.Avatar != null && core.Avatar.Spawned
                        && map.terrainGrid.FoundationAt(core.Avatar.Position)?.IsSubstructure == true);
                    break;
                case 9200:
                    Check("letter triggered a P.R.I.S.M. announcement",
                        PrismSpeech.LastSpokenTick >= ticks - 200 && !PrismSpeech.LastLine.NullOrEmpty());
                    if (!PrismSpeech.LastLine.NullOrEmpty())
                    {
                        Log.Message("[HoloAI SelfTest] announcement line: " + PrismSpeech.LastLine);
                    }
                    break;
                case 9900:
                    TestPersonaInstall(map);
                    break;
                case 10600:
                    TestPersonaAura(map);
                    break;
                case 10650:
                    TestPersonaSwitcherDialogSmoke(map);
                    StartJobDrivenInstall(map);
                    break;
                case 10700:
                    // OrderInstall picks the nearest eligible colonist — usually the
                    // test subject parked beside the matrix, but any wandering
                    // colonist can legitimately win the distance race. Assert the
                    // order landed, not who took it.
                    Check("job-driven install order accepted",
                        map.mapPawns.FreeColonistsSpawned.Any(p =>
                            p.CurJob != null && p.CurJob.def == HoloAI_DefOf.HoloAI_InstallPersona));
                    break;
                case 11300:
                    CheckJobDrivenInstall();
                    break;
                case 11400:
                    SetupWardenGiverTest(map);
                    break;
                case 11550:
                    CheckWardenGiverDueAndInsecure();
                    break;
                case 11800:
                    TestIxiaBreakFactor(map);
                    TestNeedWardenAlertSuppressed(map);
                    InstallMatrixDirect(map, "HoloAI_Matrix_ATHENA");
                    break;
                case 12200:
                    TestNeedWardenAlertControl();
                    TestAthenaSeminar();
                    InstallMatrixDirect(map, "HoloAI_Matrix_ACESO");
                    break;
                case 12700:
                    TestAcesoEmergency(map);
                    break;
                case 12900:
                    SetupHermesCleanTest(map);
                    break;
                case 13400:
                    CheckHermesClean(map);
                    core.RestoreDefaultPersona();
                    break;
                case 13800:
                    TestPrismRestoreAndTriage(map);
                    break;
            }
        }

        private Dictionary<Pawn, int> savedWardenPriorities;

        /// <summary>
        /// With I.X.I.A. projected, a shipboard prisoner, and every colonist's
        /// Warden work zeroed, the "Need warden" alert must stay quiet — she is the
        /// warden (Patch_AlertNeedWarden). The prisoner is re-secured (she was
        /// released for the insecure-giver test) so she stays put for the control
        /// half at 12200; priorities are restored there.
        /// </summary>
        private void TestNeedWardenAlertSuppressed(Map map)
        {
            if (wardenTestPrisoner == null || !wardenTestPrisoner.Spawned
                || !wardenTestPrisoner.IsPrisonerOfColony)
            {
                Check("need-warden alert suppressed while I.X.I.A. runs the brig", pass: false);
                return;
            }
            wardenTestPrisoner.guest.Released = false;
            savedWardenPriorities = new Dictionary<Pawn, int>();
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist.workSettings != null && colonist.workSettings.EverWork)
                {
                    savedWardenPriorities[colonist] = colonist.workSettings.GetPriority(WorkTypeDefOf.Warden);
                    colonist.workSettings.SetPriority(WorkTypeDefOf.Warden, 0);
                }
            }
            Check("need-warden alert suppressed while I.X.I.A. runs the brig",
                !new Alert_NeedWarden().GetReport().active);
        }

        /// <summary>
        /// Same board state after the swap to A.T.H.E.N.A.: no colonist wardens and
        /// a shipboard prisoner must alert again — proving the suppression is
        /// I.X.I.A.-specific, not a blanket mute. Warden priorities restored after.
        /// </summary>
        private void TestNeedWardenAlertControl()
        {
            if (savedWardenPriorities == null)
            {
                return; // suppressed half never armed; its Check already failed
            }
            bool prisonerPresent = wardenTestPrisoner != null && wardenTestPrisoner.Spawned
                && wardenTestPrisoner.IsPrisonerOfColony;
            Check("need-warden alert returns without I.X.I.A.",
                prisonerPresent && new Alert_NeedWarden().GetReport().active);
            foreach (KeyValuePair<Pawn, int> saved in savedWardenPriorities)
            {
                if (!saved.Key.Dead && saved.Key.workSettings != null)
                {
                    saved.Key.workSettings.SetPriority(WorkTypeDefOf.Warden, saved.Value);
                }
            }
        }

        /// <summary>
        /// Every persona that declares a speechPrefix must cover the full
        /// announcement/bark keyword set, and I.X.I.A. her signature warden acts.
        /// A missing keyword silently falls back to P.R.I.S.M.'s voice in play, so
        /// only a def-level audit like this ever catches one.
        /// </summary>
        private static bool PersonaSpeechCoverage()
        {
            bool ok = true;
            string[] roots =
            {
                "announce_threatbig", "announce_threatsmall", "announce_negative",
                "announce_lowfuel", "bark",
            };
            foreach (HoloPersonaDef persona in DefDatabase<HoloPersonaDef>.AllDefsListForReading)
            {
                if (persona.speechPrefix.NullOrEmpty())
                {
                    continue;
                }
                foreach (string root in roots)
                {
                    string keyword = persona.speechPrefix + "_" + root;
                    if (PrismSpeech.ResolveLine(keyword).NullOrEmpty())
                    {
                        Log.Message("[HoloAI SelfTest] missing/unresolvable speech keyword: " + keyword);
                        ok = false;
                    }
                }
            }
            foreach (string keyword in new[]
                { "ixia_recruit", "ixia_convert", "ixia_suppress", "ixia_feed", "ixia_prisonbreak" })
            {
                if (PrismSpeech.ResolveLine(keyword).NullOrEmpty())
                {
                    Log.Message("[HoloAI SelfTest] missing/unresolvable speech keyword: " + keyword);
                    ok = false;
                }
            }
            return ok;
        }

        /// <summary>
        /// Pawn generation with random relations disabled: relation workers roll
        /// against existing pawns, and landing on one of our NameSingle pawns
        /// (P.R.I.S.M., "HoloAI-TestSubject") crashes vanilla's
        /// PawnRelationWorker_Parent.ResolveMyName with an InvalidCastException
        /// (it casts the relative's Name to NameTriple).
        /// </summary>
        private static Pawn GenerateLoosePawn(PawnKindDef kind, Faction faction)
        {
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction,
                canGeneratePawnRelations: false));
        }

        private void Setup(Map map)
        {
            IntVec3 center = FindTestSite(map);
            siteCenter = center;
            TerrainDef substructure = DefDatabase<TerrainDef>.GetNamed("Substructure");
            foreach (IntVec3 c in CellRect.CenteredOn(center, 12))
            {
                if (c.InBounds(map))
                {
                    map.terrainGrid.SetFoundation(c, substructure);
                    foreach (Thing t in c.GetThingList(map).ListFullCopy())
                    {
                        if (t.def.destroyable && !(t is Pawn))
                        {
                            t.Destroy();
                        }
                    }
                }
            }
            // A grav engine legitimizes the substructure — without one the support
            // system collapses the patch (and the core) shortly after placement.
            Thing engine = ThingMaker.MakeThing(ThingDef.Named("GravEngine"));
            engine.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(engine, center + new IntVec3(-6, 0, -6), map);

            Thing coreThing = ThingMaker.MakeThing(HoloAI_DefOf.HoloAI_HoloCore);
            coreThing.SetFaction(Faction.OfPlayer);
            core = (Building_HoloCore)GenSpawn.Spawn(coreThing, center, map);
            testColonist = GenerateLoosePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            testColonist.Name = new NameSingle("HoloAI-TestSubject");
            GenSpawn.Spawn(testColonist, center + new IntVec3(3, 0, 0), map);
            Find.CameraDriver?.JumpToCurrentMapLoc(center);
            Log.Message("[HoloAI SelfTest] setup complete at " + center);
        }

        /// <summary>
        /// SetFoundation refuses cells with under-terrain (bridges, water beds), and a
        /// core on unsupported ground gets destroyed by the substructure system — so
        /// scan for a patch where every cell accepts a foundation.
        /// </summary>
        private static IntVec3 FindTestSite(Map map)
        {
            IntVec3 best = map.Center;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                IntVec3 candidate = attempt == 0
                    ? map.Center
                    : CellFinder.RandomCell(map);
                bool viable = true;
                foreach (IntVec3 c in CellRect.CenteredOn(candidate, 12))
                {
                    if (!c.InBounds(map) || map.terrainGrid.UnderTerrainAt(c) != null
                        || c.GetTerrain(map).IsWater)
                    {
                        viable = false;
                        break;
                    }
                }
                if (viable)
                {
                    return candidate;
                }
            }
            Log.Warning("[HoloAI SelfTest] no fully viable test site found; using map center");
            return best;
        }

        private void TestUnattackable()
        {
            Pawn_HoloAvatar avatar = core.Avatar;
            if (avatar == null || !avatar.Spawned)
            {
                Check("avatar unattackable (AI ThreatDisabled)", pass: false);
                return;
            }
            Check("avatar unattackable (AI ThreatDisabled)", avatar.ThreatDisabled(null));
            System.Reflection.MethodInfo canTarget = AccessTools.Method(
                typeof(FloatMenuOptionProvider_DraftedAttack), "CanTarget");
            if (canTarget == null)
            {
                Check("avatar unattackable (force-attack menu)", pass: false);
                return;
            }
            bool result = (bool)canTarget.Invoke(null, new object[] { avatar });
            Check("avatar unattackable (force-attack menu)", !result);
        }

        private void TestStylingTrackers()
        {
            Pawn_HoloAvatar avatar = core.Avatar;
            if (!ModsConfig.IdeologyActive || avatar == null || !avatar.Spawned)
            {
                Log.Message("[HoloAI SelfTest] styling test skipped (no Ideology or no avatar)");
                return;
            }
            try
            {
                Color colorBefore = avatar.HoloHairColor;
                avatar.AttachStyleTrackers();
                _ = new Dialog_HoloStyling(avatar);
                avatar.DetachStyleTrackers();
                Check("styling dialog constructs and trackers detach",
                    avatar.story == null && avatar.style == null);
                // Regression: attach/detach with no user picks (= opening the dialog
                // and hitting cancel) must not strip the persona hair color.
                Check("styling round-trip preserves hair color",
                    avatar.HoloHairColor == colorBefore);
            }
            catch (System.Exception e)
            {
                Log.Message("[HoloAI SelfTest] styling dialog threw: " + e);
                Check("styling dialog constructs and trackers detach", pass: false);
                Check("styling round-trip preserves hair color", pass: false);
            }
        }

        /// <summary>Regression: a minified holocore must tick without exceptions
        /// (MinifiedThing forwards ticks to the inner, unspawned building).</summary>
        private void SpawnMinifiedCore(Map map)
        {
            Thing inner = ThingMaker.MakeThing(HoloAI_DefOf.HoloAI_HoloCore);
            inner.SetFaction(Faction.OfPlayer);
            MinifiedThing mini = inner.MakeMinified();
            GenSpawn.Spawn(mini, siteCenter + new IntVec3(5, 0, -5), map);
            Log.Message("[HoloAI SelfTest] minified holocore spawned (tick-safety regression)");
        }

        private void StartTeleportTest(Map map)
        {
            Pawn_HoloAvatar avatar = core.Avatar;
            if (avatar == null || !avatar.Spawned)
            {
                return;
            }
            // Drop her on bare ground outside the patch; the 5s ship-bound rule
            // should snap her back near the core well before the 9800 check.
            if (CellFinder.TryFindRandomCellNear(siteCenter, map, 40,
                    c => c.Standable(map) && map.terrainGrid.FoundationAt(c) == null
                        && !c.InHorDistOf(siteCenter, 15f),
                    out IntVec3 offShip))
            {
                avatar.jobs?.StopAll();
                avatar.Position = offShip;
                avatar.Notify_Teleported();
                Log.Message("[HoloAI SelfTest] avatar exiled to " + offShip + " for teleport test");
            }
        }

        private void TestPersonaInstall(Map map)
        {
            ThingDef matrixDef = DefDatabase<ThingDef>.GetNamedSilentFail("HoloAI_Matrix_HERMES");
            if (matrixDef == null)
            {
                Check("persona matrix def exists", pass: false);
                return;
            }
            Check("persona matrix def exists", pass: true);
            Thing matrix = GenSpawn.Spawn(ThingMaker.MakeThing(matrixDef),
                siteCenter + new IntVec3(-3, 0, 0), map);
            core.InstallPersona(matrix);
            Check("persona installed (H.E.R.M.E.S. active)",
                core.ActivePersona?.defName == "HoloAI_Persona_HERMES");
            Check("consumed matrix destroyed", matrix.Destroyed);
        }

        private void TestPersonaAura(Map map)
        {
            Check("new avatar renamed to persona",
                core.Avatar != null && core.Avatar.Spawned
                && core.Avatar.Name?.ToStringFull == "H.E.R.M.E.S.");

            HediffDef aura = core.ActivePersona?.auraHediff;
            bool auraSeen = false;
            float speedBefore = -1f, speedAfter = -1f;
            if (aura != null)
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
                {
                    Hediff h = colonist.health.hediffSet.GetFirstHediffOfDef(aura);
                    if (h != null)
                    {
                        auraSeen = true;
                        speedAfter = colonist.GetStatValue(StatDefOf.MoveSpeed);
                        h.Severity = 0f;
                        colonist.health.RemoveHediff(h);
                        speedBefore = colonist.GetStatValue(StatDefOf.MoveSpeed);
                        break;
                    }
                }
            }
            Check("aura hediff applied to crew on ship", auraSeen);
            Check("aura grants move speed", auraSeen && speedAfter > speedBefore);

            // Install a second persona: the archive model consumes the new matrix,
            // ejects nothing, and keeps the outgoing persona unlocked.
            ThingDef vestaDef = DefDatabase<ThingDef>.GetNamedSilentFail("HoloAI_Matrix_VESTA");
            Thing vesta = GenSpawn.Spawn(ThingMaker.MakeThing(vestaDef),
                siteCenter + new IntVec3(-3, 0, 2), map);
            core.InstallPersona(vesta);
            bool ejected = false;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(core.Position, 6f, useCenter: true))
            {
                if (c.InBounds(map) && c.GetThingList(map)
                        .Any(t => t.def.defName == "HoloAI_Matrix_HERMES"))
                {
                    ejected = true;
                    break;
                }
            }
            Check("no matrix ejected on swap (archive model)", vesta.Destroyed && !ejected);
            Check("outgoing persona stays archived",
                core.IsUnlocked(HoloAI_DefOf.HoloAI_Persona_HERMES));

            // The archive's whole point: switch back to H.E.R.M.E.S. with no matrix
            // anywhere on the map.
            core.ActivatePersona(HoloAI_DefOf.HoloAI_Persona_HERMES);
            Check("archived persona reactivates without a matrix",
                core.ActivePersona == HoloAI_DefOf.HoloAI_Persona_HERMES);
        }

        /// <summary>
        /// Opens the persona-switcher dialog with a populated archive (H.E.R.M.E.S.
        /// and V.E.S.T.A. were both installed by earlier checkpoints) and closes it a
        /// few ticks later. Any exception thrown while the window is constructed,
        /// laid out, or drawn surfaces in Player.log as a stack trace with a
        /// ShipHoloAI frame — a clean run with none is the render smoke-test signal
        /// since this harness has no way to eyeball pixels.
        /// </summary>
        private void TestPersonaSwitcherDialogSmoke(Map map)
        {
            try
            {
                Check("dialog smoke-test has archived personas to list",
                    core.IsUnlocked(HoloAI_DefOf.HoloAI_Persona_VESTA)
                    && core.IsUnlocked(HoloAI_DefOf.HoloAI_Persona_HERMES));
                Dialog_PersonaSwitcher dialog = new Dialog_PersonaSwitcher(core);
                Find.WindowStack.Add(dialog);
                Find.WindowStack.TryRemove(dialog, doCloseSound: false);
                Check("persona switcher dialog constructs and closes without throwing", pass: true);
            }
            catch (System.Exception e)
            {
                Log.Message("[HoloAI SelfTest] persona switcher dialog threw: " + e);
                Check("persona switcher dialog constructs and closes without throwing", pass: false);
            }
        }

        /// <summary>
        /// Exercises the actual JobDriver_InstallPersona toil chain end to end — goto
        /// matrix, carry it, goto core, wait 240 ticks, install — rather than calling
        /// Building_HoloCore.InstallPersona directly like TestPersonaInstall does. This
        /// is the path a player's colonist takes via the gizmo/dialog, and the specific
        /// path that silently died before the FailOnDespawnedOrNull(TargetIndex.A) ->
        /// FailOnDestroyedOrNull(TargetIndex.A) fix (the matrix legitimately despawns
        /// from the map the moment it's carried).
        /// </summary>
        private void StartJobDrivenInstall(Map map)
        {
            ThingDef matrixDef = DefDatabase<ThingDef>.GetNamedSilentFail("HoloAI_Matrix_ATHENA");
            if (matrixDef == null || testColonist == null || !testColonist.Spawned)
            {
                Check("job-driven install matrix spawned", pass: false);
                return;
            }
            IntVec3 matrixPos = siteCenter + new IntVec3(-3, 0, -3);
            jobInstallMatrix = GenSpawn.Spawn(ThingMaker.MakeThing(matrixDef), matrixPos, map);
            Check("job-driven install matrix spawned", jobInstallMatrix != null && !jobInstallMatrix.Destroyed);

            // Guarantee OrderInstall's nearest-reachable-colonist search finds her: put
            // her in a normal, idle state right next to the matrix.
            testColonist.jobs?.StopAll();
            if (testColonist.Downed)
            {
                testColonist.health.Reset();
            }
            if (testColonist.Drafted)
            {
                testColonist.drafter.Drafted = false;
            }
            testColonist.Position = matrixPos + new IntVec3(1, 0, 0);

            core.OrderInstall(jobInstallMatrix);
        }

        private void CheckJobDrivenInstall()
        {
            Check("job-driven install completed (persona swapped)",
                core.ActivePersona?.defName == "HoloAI_Persona_ATHENA");
            Check("job-driven install completed (matrix consumed)",
                jobInstallMatrix != null && jobInstallMatrix.Destroyed);
        }

        /// <summary>
        /// Confirms JobGiver_HoloWarden no-ops for the currently active (non-I.X.I.A.)
        /// persona, then installs I.X.I.A., builds real holding rooms, and plants a
        /// due prisoner (secure in her cell, awake, AttemptRecruit) plus a
        /// not-yet-due slave for the next checkpoints to find.
        /// </summary>
        private void SetupWardenGiverTest(Map map)
        {
            JobGiver_HoloWarden giver = new JobGiver_HoloWarden();
            Check("JobGiver_HoloWarden no-op for non-I.X.I.A. persona",
                core.Avatar != null && core.Avatar.Spawned
                && core.ActivePersona?.defName != "HoloAI_Persona_IXIA"
                && giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams)).Job == null);

            ThingDef ixiaMatrixDef = DefDatabase<ThingDef>.GetNamedSilentFail("HoloAI_Matrix_IXIA");
            if (ixiaMatrixDef == null)
            {
                Check("I.X.I.A. matrix def exists", pass: false);
                return;
            }
            Thing ixiaMatrix = GenSpawn.Spawn(ThingMaker.MakeThing(ixiaMatrixDef),
                siteCenter + new IntVec3(4, 0, -4), map);
            core.InstallPersona(ixiaMatrix);

            BuildHoldingRooms(map);

            // Must belong to a real hostile faction: capturing a same-faction pawn
            // leaves Pawn_GuestTracker.PrisonerIsSecure false (HostFaction never
            // legitimately attaches when host == the pawn's own faction). She also
            // has to live in a genuine prison cell — captured on open deck she runs
            // a vanilla exit-map escape job, which flips PrisonerIsSecure false.
            Faction hostileFaction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            wardenTestPrisoner = GenerateLoosePawn(PawnKindDefOf.Villager, hostileFaction);
            GenSpawn.Spawn(wardenTestPrisoner, siteCenter + new IntVec3(-9, 0, 1), map);
            wardenTestPrisoner.guest.CapturedBy(Faction.OfPlayer);
            wardenTestPrisoner.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.AttemptRecruit);
            wardenTestPrisoner.jobs?.StopAll();

            if (ModsConfig.IdeologyActive)
            {
                wardenTestSlave = GenerateLoosePawn(PawnKindDefOf.Villager, hostileFaction);
                GenSpawn.Spawn(wardenTestSlave, siteCenter + new IntVec3(-9, 0, 6), map);
                wardenTestSlave.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
                // Parked as not-due: the 11550 prisoner assertions need the prisoner
                // to be the only candidate; TestWardenSlaveSuppression arms her later.
                wardenTestSlave.guest.slaveInteractionMode = SlaveInteractionModeDefOf.NoInteraction;
                wardenTestSlave.jobs?.StopAll();
            }
        }

        /// <summary>
        /// Two roofed 5x5 rooms — a prison cell and slave quarters, each with an
        /// owner-typed bed — on a substructure spur north-west of the patch, joined
        /// to it by a bare corridor column. Holding the warden-test pawns in real
        /// rooms keeps them off vanilla escape jobs (exitMapOnArrival is what breaks
        /// PrisonerIsSecure/SlaveIsSecure), so warden eligibility is deterministic
        /// and nobody wanders off the map mid-test. The farthest cell sits ~16 cells
        /// from the grav engine, inside its 18.9 substructure support radius.
        /// </summary>
        private void BuildHoldingRooms(Map map)
        {
            var prisonRect = new CellRect(siteCenter.x - 12, siteCenter.z, 5, 5);
            var slaveRect = new CellRect(siteCenter.x - 12, siteCenter.z + 5, 5, 5);
            TerrainDef substructure = DefDatabase<TerrainDef>.GetNamed("Substructure");
            var corridor = Enumerable.Range(0, 10)
                .Select(i => new IntVec3(siteCenter.x - 7, 0, siteCenter.z + i));
            foreach (IntVec3 c in prisonRect.Cells.Concat(slaveRect.Cells).Concat(corridor))
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
            BuildRoom(map, prisonRect, new IntVec3(siteCenter.x - 8, 0, siteCenter.z + 2),
                BedOwnerType.Prisoner);
            BuildRoom(map, slaveRect, new IntVec3(siteCenter.x - 8, 0, siteCenter.z + 7),
                ModsConfig.IdeologyActive ? BedOwnerType.Slave : BedOwnerType.Colonist);
        }

        private static void BuildRoom(Map map, CellRect rect, IntVec3 doorCell, BedOwnerType bedFor)
        {
            foreach (IntVec3 c in rect.EdgeCells)
            {
                if (!c.InBounds(map) || c == doorCell)
                {
                    continue;
                }
                Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                wall.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(wall, c, map);
            }
            Thing door = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.Steel);
            door.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(door, doorCell, map);
            foreach (IntVec3 c in rect)
            {
                if (c.InBounds(map))
                {
                    map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                }
            }
            var bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.Steel);
            bed.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bed, rect.CenterCell, map, Rot4.South);
            bed.ForOwnerType = bedFor;
        }

        /// <summary>
        /// With I.X.I.A. reprojected and a due prisoner planted, JobGiver_HoloWarden
        /// must hand back a real HoloAI_WardenInteract job targeting her. Then the
        /// prisoner is made insecure (guest.Released) — the same "not actually due"
        /// family as asleep/downed — and the giver must go back to returning null.
        /// </summary>
        private void CheckWardenGiverDueAndInsecure()
        {
            Check("avatar reprojected as I.X.I.A.",
                core.Avatar != null && core.Avatar.Spawned
                && core.ActivePersona?.defName == "HoloAI_Persona_IXIA");

            if (core.Avatar == null || !core.Avatar.Spawned || wardenTestPrisoner == null)
            {
                Check("JobGiver_HoloWarden returns a job for a due prisoner", pass: false);
                Check("JobGiver_HoloWarden returns null once the candidate is insecure", pass: false);
                TestWardenSlaveSuppression();
                return;
            }

            // A hungry prisoner in a real cell would divert the giver to its
            // food-delivery branch (which deliberately outranks interaction), so
            // keep her fed for a deterministic interaction assertion.
            if (wardenTestPrisoner.needs?.food != null)
            {
                wardenTestPrisoner.needs.food.CurLevel = wardenTestPrisoner.needs.food.MaxLevel;
            }

            Log.Message("[HoloAI SelfTest] warden diag: IsPrisonerOfColony=" + wardenTestPrisoner.IsPrisonerOfColony
                + " PrisonerIsSecure=" + wardenTestPrisoner.guest.PrisonerIsSecure
                + " Spawned=" + wardenTestPrisoner.Spawned
                + " InAggroMentalState=" + wardenTestPrisoner.InAggroMentalState
                + " IsForbidden=" + wardenTestPrisoner.IsForbidden(core.Avatar)
                + " IsFormingCaravan=" + wardenTestPrisoner.IsFormingCaravan()
                + " ExclusiveInteractionMode=" + wardenTestPrisoner.guest.ExclusiveInteractionMode?.defName
                + " ScheduledForInteraction=" + wardenTestPrisoner.guest.ScheduledForInteraction
                + " InPrisonCell=" + wardenTestPrisoner.Position.IsInPrisonCell(wardenTestPrisoner.Map)
                + " Downed=" + wardenTestPrisoner.Downed
                + " Awake=" + wardenTestPrisoner.Awake()
                + " CanReach=" + core.Avatar.CanReach(wardenTestPrisoner, PathEndMode.Touch, Danger.None)
                + " Position=" + wardenTestPrisoner.Position + " AvatarPos=" + core.Avatar.Position
                + " lastAssignedInteractTime=" + wardenTestPrisoner.mindState.lastAssignedInteractTime
                + " interactionsToday=" + wardenTestPrisoner.mindState.interactionsToday
                + " TicksGame=" + Find.TickManager.TicksGame);

            JobGiver_HoloWarden giver = new JobGiver_HoloWarden();
            ThinkResult due = giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("JobGiver_HoloWarden returns a job for a due prisoner",
                due.Job != null && due.Job.def == HoloAI_DefOf.HoloAI_WardenInteract
                && due.Job.GetTarget(TargetIndex.A).Thing == wardenTestPrisoner);

            TestWardenConversion(giver);

            wardenTestPrisoner.guest.Released = true; // PrisonerIsSecure -> false
            ThinkResult afterInsecure = giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("JobGiver_HoloWarden returns null once the candidate is insecure",
                afterInsecure.Job == null);

            TestWardenSlaveSuppression();
        }

        /// <summary>
        /// Flip the same prisoner to Convert mode with the player ideo as the goal:
        /// the giver must still fire even though I.X.I.A. has no ideoligion of her
        /// own (the goal is read off the guest tracker, not the warden), and a fired
        /// attempt must actually drain certainty toward it. Mode is left on Convert
        /// afterwards — the release assertion that follows blocks on PrisonerIsSecure
        /// long before the interaction mode matters.
        /// </summary>
        private void TestWardenConversion(JobGiver_HoloWarden giver)
        {
            if (!ModsConfig.IdeologyActive || Find.IdeoManager.classicMode)
            {
                Log.Message("[HoloAI SelfTest] conversion test skipped (no Ideology / classic mode)");
                return;
            }
            Ideo goal = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (goal == null || wardenTestPrisoner.ideo == null)
            {
                Check("JobGiver_HoloWarden targets a convert-mode prisoner", pass: false);
                Check("conversion attempt drains certainty toward the goal ideo", pass: false);
                return;
            }
            if (wardenTestPrisoner.Ideo == goal)
            {
                Ideo other = Find.IdeoManager.IdeosListForReading.FirstOrDefault(i => i != goal);
                if (other == null)
                {
                    Log.Message("[HoloAI SelfTest] conversion test skipped (only one ideoligion in game)");
                    return;
                }
                wardenTestPrisoner.ideo.SetIdeo(other);
            }
            wardenTestPrisoner.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Convert);
            wardenTestPrisoner.guest.ideoForConversion = goal;

            ThinkResult convertDue = giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("JobGiver_HoloWarden targets a convert-mode prisoner",
                convertDue.Job != null && convertDue.Job.def == HoloAI_DefOf.HoloAI_WardenInteract
                && convertDue.Job.GetTarget(TargetIndex.A).Thing == wardenTestPrisoner);

            float certaintyBefore = wardenTestPrisoner.ideo.Certainty;
            bool converted = JobDriver_HoloWarden.TryConvertPrisoner(core.Avatar, wardenTestPrisoner);
            Check("conversion attempt drains certainty toward the goal ideo",
                wardenTestPrisoner.ideo.Certainty < certaintyBefore
                || (converted && wardenTestPrisoner.Ideo == goal));

            // The attempt must stamp vanilla's interaction cooldown — a giver that
            // still fires here would let her chain attempts and trivialize conversion.
            Check("conversion attempt stamps the prisoner interaction cooldown",
                !wardenTestPrisoner.guest.ScheduledForInteraction
                && giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams)).Job == null);
        }

        /// <summary>
        /// With the prisoner released (insecure) the slave quarters hold the only
        /// remaining candidate: arm her — Suppress mode, drained and overdue
        /// suppression need — and the giver must target her; sate the need and it
        /// must go quiet again. Mirrors WorkGiver_Warden_SuppressSlave's gates.
        /// </summary>
        private void TestWardenSlaveSuppression()
        {
            if (!ModsConfig.IdeologyActive)
            {
                Log.Message("[HoloAI SelfTest] slave suppression test skipped (no Ideology)");
                return;
            }
            if (wardenTestSlave == null || !wardenTestSlave.Spawned
                || core.Avatar == null || !core.Avatar.Spawned)
            {
                Check("JobGiver_HoloWarden suppresses a due slave", pass: false);
                Check("JobGiver_HoloWarden returns null once the slave is sated", pass: false);
                return;
            }

            wardenTestSlave.guest.slaveInteractionMode = SlaveInteractionModeDefOf.Suppress;
            wardenTestSlave.mindState.lastSlaveSuppressedTick = -99999;
            bool hasNeed = wardenTestSlave.needs.TryGetNeed(out Need_Suppression suppression);
            if (hasNeed)
            {
                suppression.CurLevel = 0.2f; // CanBeSuppressedNow needs < 0.7
            }
            Log.Message("[HoloAI SelfTest] slave diag: IsSlaveOfColony=" + wardenTestSlave.IsSlaveOfColony
                + " SlaveIsSecure=" + wardenTestSlave.guest.SlaveIsSecure
                + " hasSuppressionNeed=" + hasNeed
                + " ScheduledForSlaveSuppression=" + wardenTestSlave.guest.ScheduledForSlaveSuppression
                + " Downed=" + wardenTestSlave.Downed
                + " Awake=" + wardenTestSlave.Awake()
                + " Position=" + wardenTestSlave.Position);

            JobGiver_HoloWarden giver = new JobGiver_HoloWarden();
            ThinkResult due = giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("JobGiver_HoloWarden suppresses a due slave",
                due.Job != null && due.Job.def == HoloAI_DefOf.HoloAI_WardenInteract
                && due.Job.GetTarget(TargetIndex.A).Thing == wardenTestSlave);

            if (hasNeed)
            {
                suppression.CurLevel = 1f; // sated -> CanBeSuppressedNow false
            }
            Check("JobGiver_HoloWarden returns null once the slave is sated",
                giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams)).Job == null);

            TestWardenFeedDowned(giver);
        }

        /// <summary>
        /// Down the (sated) slave on the floor of her quarters and starve her, with a
        /// meal stack planted on the ship's substructure: the giver must return the
        /// hand-feed job (downed-out-of-bed is exactly the case vanilla's in-a-bed
        /// feed gates exclude and ours must cover), and the driver's
        /// materialize-and-feed must actually raise her food need.
        /// </summary>
        private void TestWardenFeedDowned(JobGiver_HoloWarden giver)
        {
            // Anesthetic, not DamageUntilDowned: random damage occasionally fails
            // to down (or kills) the slave, flaking both checks — full sedation
            // downs her deterministically with zero kill risk.
            Hediff sedation = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, wardenTestSlave);
            sedation.Severity = 1f;
            wardenTestSlave.health.AddHediff(sedation);
            if (!wardenTestSlave.Downed || wardenTestSlave.Dead || wardenTestSlave.needs?.food == null)
            {
                Log.Message("[HoloAI SelfTest] feed diag: Downed=" + wardenTestSlave.Downed
                    + " Dead=" + wardenTestSlave.Dead + " hasFood=" + (wardenTestSlave.needs?.food != null));
                Check("JobGiver_HoloWarden hand-feeds a downed slave", pass: false);
                Check("materialize-and-feed raises the patient's food need", pass: false);
                return;
            }
            wardenTestSlave.needs.food.CurLevel = 0f;
            Thing meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
            meal.stackCount = 5;
            GenPlace.TryPlaceThing(meal, siteCenter + new IntVec3(-3, 0, -3),
                wardenTestSlave.Map, ThingPlaceMode.Near, out Thing placedMeal);

            ThinkResult due = giver.TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("JobGiver_HoloWarden hand-feeds a downed slave",
                due.Job != null && due.Job.def == HoloAI_DefOf.HoloAI_WardenFeed
                && due.Job.GetTarget(TargetIndex.A).Thing == wardenTestSlave);

            float foodBefore = wardenTestSlave.needs.food.CurLevel;
            bool fed = placedMeal != null
                && JobDriver_HoloWardenFeed.MaterializeAndFeed(core.Avatar, wardenTestSlave, placedMeal);
            Check("materialize-and-feed raises the patient's food need",
                fed && wardenTestSlave.needs.food.CurLevel > foodBefore);
        }

        /// <summary>Direct persona swap used between ability tests (the job-driven
        /// install path already has its own dedicated test at 10650-11300).</summary>
        private void InstallMatrixDirect(Map map, string matrixDefName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(matrixDefName);
            if (def == null)
            {
                Check("matrix def exists: " + matrixDefName, pass: false);
                return;
            }
            Thing matrix = GenSpawn.Spawn(ThingMaker.MakeThing(def), siteCenter + new IntVec3(-4, 0, 0), map);
            core.InstallPersona(matrix);
        }

        /// <summary>
        /// I.X.I.A.'s Thousand-Locked Gate: x4 prison-break MTB while she walks the
        /// ship. Cheap unit check on the patch's factor logic — the full
        /// InitiatePrisonBreakMtbDays integration (needs a real prison cell) lives in
        /// HoloAI_IxiaRoomTest. The prisoner was Released at 11550, so anchor them
        /// back onto the patch first; the factor only reads position + persona.
        /// </summary>
        private void TestIxiaBreakFactor(Map map)
        {
            Check("ProjectedPersona cache holds I.X.I.A.",
                HoloAuraMapComponent.ProjectedOn(map) == HoloAI_DefOf.HoloAI_Persona_IXIA);

            if (wardenTestPrisoner != null && wardenTestPrisoner.Spawned)
            {
                if (map.terrainGrid.FoundationAt(wardenTestPrisoner.Position)?.IsSubstructure != true)
                {
                    wardenTestPrisoner.Position = siteCenter + new IntVec3(4, 0, -3);
                    wardenTestPrisoner.Notify_Teleported();
                }
                Check("break MTB factor x4 for on-ship pawn under I.X.I.A.",
                    Patch_PrisonBreakMtb.BreakMtbFactorFor(wardenTestPrisoner) == Patch_PrisonBreakMtb.MtbFactor);
            }
            else
            {
                Log.Message("[HoloAI SelfTest] break-factor positive check skipped (prisoner gone)");
            }
        }

        /// <summary>A.T.H.E.N.A.: giver targets an idle colonist; the seminar lands
        /// XP through the vanilla learning pipeline plus the cooldown memory.</summary>
        private void TestAthenaSeminar()
        {
            Check("avatar reprojected as A.T.H.E.N.A.",
                core.Avatar != null && core.Avatar.Spawned
                && core.ActivePersona?.defName == "HoloAI_Persona_ATHENA");
            if (core.Avatar == null || !core.Avatar.Spawned || testColonist == null || !testColonist.Spawned)
            {
                Check("seminar giver targets idle colonist", pass: false);
                Check("seminar grants XP and cooldown memory", pass: false);
                return;
            }

            // Deterministic idleness: IsIdle reads mindState.lastJobTag. Anchor the
            // student back on the patch (free wandering can carry her off the
            // substructure, which disqualifies her), and make every other colonist
            // non-idle so she is the giver's only eligible student.
            Map seminarMap = testColonist.Map;
            testColonist.Position = core.Position + new IntVec3(-2, 0, 0);
            testColonist.Notify_Teleported();
            testColonist.jobs?.StopAll();
            testColonist.mindState.lastJobTag = JobTag.Idle;
            foreach (Pawn other in seminarMap.mapPawns.FreeColonistsSpawned)
            {
                if (other == testColonist)
                {
                    continue;
                }
                if (other.CurJob?.def.joyKind != null)
                {
                    other.jobs?.StopAll();
                }
                if (other.mindState != null)
                {
                    other.mindState.lastJobTag = JobTag.Misc;
                }
            }
            Log.Message("[HoloAI SelfTest] seminar diag: onSubstructure="
                + (seminarMap.terrainGrid.FoundationAt(testColonist.Position)?.IsSubstructure == true)
                + " isDue=" + JobGiver_HoloSeminar.IsDueStudent(core.Avatar, testColonist)
                + " Position=" + testColonist.Position);

            ThinkResult result = new JobGiver_HoloSeminar().TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("seminar giver targets idle colonist",
                result.Job != null && result.Job.def == HoloAI_DefOf.HoloAI_Seminar
                && result.Job.GetTarget(TargetIndex.A).Thing == testColonist);

            SkillRecord skill = JobDriver_HoloSeminar.PickSkill(testColonist);
            int levelBefore = skill?.Level ?? 0;
            float xpBefore = skill?.xpSinceLastLevel ?? 0f;
            // Drain joy first so a full bar can't mask the recreation grant.
            if (testColonist.needs?.joy != null)
            {
                testColonist.needs.joy.CurLevel = 0.3f;
            }
            JobDriver_HoloSeminar.FireSeminar(core.Avatar, testColonist);
            bool xpGained = skill != null
                && (skill.Level > levelBefore || skill.xpSinceLastLevel > xpBefore);
            bool memory = testColonist.needs?.mood?.thoughts?.memories?
                .GetFirstMemoryOfDef(HoloAI_DefOf.HoloAI_AttendedSeminar) != null;
            Check("seminar grants XP and cooldown memory", xpGained && memory);
            Check("seminar counts as recreation (joy gained)",
                testColonist.needs?.joy != null && testColonist.needs.joy.CurLevel > 0.3f);
            Log.Message("[HoloAI SelfTest] seminar skill: " + skill?.def.defName
                + " xp " + xpBefore + " -> " + skill?.xpSinceLastLevel + " (level " + levelBefore + " -> " + skill?.Level + ")");
        }

        /// <summary>A.C.E.S.O.: a bleeding colonist with no doctor en route is her
        /// emergency; the tend must land at the calibrated bare-hands quality.</summary>
        private void TestAcesoEmergency(Map map)
        {
            Check("avatar reprojected as A.C.E.S.O.",
                core.Avatar != null && core.Avatar.Spawned
                && core.ActivePersona?.defName == "HoloAI_Persona_ACESO");
            if (core.Avatar == null || !core.Avatar.Spawned || testColonist == null || !testColonist.Spawned)
            {
                Check("medic giver targets bleeding colonist", pass: false);
                Check("emergency tend applied at calibrated quality", pass: false);
                return;
            }

            testColonist.jobs?.StopAll(); // release any self-tend reservation
            testColonist.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f));
            testColonist.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f));

            bool emergency = JobGiver_HoloMedic.IsEmergency(core.Avatar, testColonist);
            ThinkResult result = new JobGiver_HoloMedic().TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("medic giver targets bleeding colonist",
                emergency && result.Job != null && result.Job.def == HoloAI_DefOf.HoloAI_EmergencyTend
                && result.Job.GetTarget(TargetIndex.A).Thing == testColonist);

            JobDriver_HoloMedic.FireEmergencyTend(core.Avatar, testColonist);
            bool tended = false;
            float quality = -1f;
            foreach (Hediff hediff in testColonist.health.hediffSet.hediffs)
            {
                HediffComp_TendDuration tend = hediff.TryGetComp<HediffComp_TendDuration>();
                if (tend != null && tend.IsTended)
                {
                    tended = true;
                    quality = tend.tendQuality;
                    break;
                }
            }
            // 0.75 statBase x 0.60 herbal potency = 0.45 base (+-0.25 random swing)
            // — below 0.2 means the conjured dose regressed to bare hands, above
            // 0.75 means the calibration broke upward.
            Check("emergency tend applied at calibrated quality",
                tended && quality >= 0.2f && quality <= 0.75f);
            // No cooldown: wound her again and the giver must answer immediately.
            // The wound is injected directly — TakeDamage can be absorbed by armor
            // or damage-model mods on heavy modlists, silently voiding the check.
            Hediff_Injury refireCut = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, testColonist);
            refireCut.Severity = 8f;
            testColonist.health.AddHediff(refireCut,
                testColonist.RaceProps.body.corePart);
            ThinkResult again = new JobGiver_HoloMedic().TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("emergency tend has no cooldown (giver re-fires immediately)",
                again.Job != null && again.Job.def == HoloAI_DefOf.HoloAI_EmergencyTend);
            Log.Message("[HoloAI SelfTest] emergency tend quality: " + quality);
        }

        /// <summary>
        /// Plant filth near the core and install H.E.R.M.E.S. — CheckHermesClean then
        /// asserts the think-tree loop actually sent her to scrub it, making this a
        /// true end-to-end test of giver, driver, and toil chain (500 ticks is ample:
        /// short path at Jog + ~60 ticks/thickness of scrubbing, even with the blood
        /// the A.C.E.S.O. test left on the deck queued ahead of it).
        /// </summary>
        private void SetupHermesCleanTest(Map map)
        {
            ThingDef filthDef = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Dirt");
            IntVec3 cell = siteCenter + new IntVec3(2, 0, 2);
            if (filthDef == null || !FilthMaker.TryMakeFilth(cell, map, filthDef, 2))
            {
                Check("test filth spawned on substructure", pass: false);
                return;
            }
            testFilth = cell.GetFirstThing<Filth>(map);
            Check("test filth spawned on substructure",
                testFilth != null
                && map.terrainGrid.FoundationAt(testFilth.Position)?.IsSubstructure == true);
            InstallMatrixDirect(map, "HoloAI_Matrix_HERMES");
        }

        private void CheckHermesClean(Map map)
        {
            Check("avatar reprojected as H.E.R.M.E.S.",
                core.Avatar != null && core.Avatar.Spawned
                && core.ActivePersona?.defName == "HoloAI_Persona_HERMES");
            Check("ProjectedPersona cache holds H.E.R.M.E.S.",
                HoloAuraMapComponent.ProjectedOn(map) == HoloAI_DefOf.HoloAI_Persona_HERMES);
            Check("H.E.R.M.E.S. cleaned the planted filth (end-to-end)",
                testFilth == null || testFilth.Destroyed);
            // Belt and braces: the giver itself must also no-op once the ship is clean
            // (or target remaining filth if the A.C.E.S.O. blood outlasted the window).
            if (core.Avatar != null && core.Avatar.Spawned)
            {
                Filth remaining = JobGiver_HoloClean.FindFilth(core.Avatar);
                Log.Message("[HoloAI SelfTest] remaining ship filth after clean window: "
                    + (remaining == null ? "none" : remaining.def.defName + " at " + remaining.Position));
            }
        }

        /// <summary>
        /// The RestoreDefaultPersona path (fired at 13400): P.R.I.S.M. returns without
        /// a matrix, the outgoing persona stays in the archive, and her companion-triage
        /// chat prefers whoever is near breaking. Also the V.E.S.T.A. tuck-in effect
        /// (fired directly — real sleep timing is too fragile for the harness) and the
        /// break-factor negative control now that I.X.I.A. is long gone.
        /// </summary>
        private void TestPrismRestoreAndTriage(Map map)
        {
            Check("RestoreDefaultPersona reverts to P.R.I.S.M.",
                core.ActivePersona?.defName == "HoloAI_Persona_PRISM"
                && core.Avatar != null && core.Avatar.Spawned
                && core.Avatar.Name?.ToStringFull == "P.R.I.S.M.");
            Check("restore keeps the outgoing persona archived",
                core.IsUnlocked(HoloAI_DefOf.HoloAI_Persona_HERMES));
            // The hairstyle cycled at tick 1400 must survive the whole persona
            // carousel: PRISM -> HERMES -> ... -> PRISM regenerates a fresh pawn,
            // so this passes only if the core's style memory reapplied it.
            Check("player restyle survives persona swaps",
                restyledPrismHair != null && core.Avatar != null
                && core.Avatar.CurrentHairDef == restyledPrismHair);
            Check("break MTB factor back to x1 without I.X.I.A.",
                testColonist == null || !testColonist.Spawned
                || Patch_PrisonBreakMtb.BreakMtbFactorFor(testColonist) == 1f);

            if (core.Avatar == null || !core.Avatar.Spawned || testColonist == null || !testColonist.Spawned)
            {
                Check("triage chat targets the near-break colonist", pass: false);
                Check("tuck-in effect grants memory", pass: false);
                return;
            }

            // A content second colonist to compete for the chat; the miserable
            // original must win under triage.
            Pawn happy = GenerateLoosePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            happy.Name = new NameSingle("HoloAI-Happy");
            GenSpawn.Spawn(happy, core.Position + new IntVec3(-3, 0, 1), map);
            testColonist.Position = core.Position + new IntVec3(3, 0, 1);
            testColonist.needs.mood.CurLevel = 0.02f;
            core.Avatar.nextChatTick = 0;
            ThinkResult chat = new JobGiver_HoloChat().TryIssueJobPackage(core.Avatar, default(JobIssueParams));
            Check("triage chat targets the near-break colonist",
                JobGiver_HoloChat.NearBreak(testColonist)
                && chat.Job != null && chat.Job.def == HoloAI_DefOf.HoloAI_Chat
                && chat.Job.GetTarget(TargetIndex.A).Thing == testColonist);

            JobDriver_HoloTuckIn.FireTuckIn(core.Avatar, testColonist);
            Check("tuck-in effect grants memory",
                testColonist.needs?.mood?.thoughts?.memories?
                    .GetFirstMemoryOfDef(HoloAI_DefOf.HoloAI_TuckedInByVESTA) != null);
        }

        private void ForcePower(bool on)
        {
            CompPowerTrader comp = core?.GetComp<CompPowerTrader>();
            if (comp != null && comp.PowerOn != on)
            {
                comp.PowerOn = on;
            }
        }

        private void ObserveChat()
        {
            // The colonist AI may walk them off the test patch; anchor them near the
            // core so the avatar gets her chance to chat. Stop once the chat evidence
            // is in, and never fight a player who drafted the pawn mid-test.
            bool stillNeedChat = !chatMemoryObserved || !chatLogObserved;
            if (stillNeedChat && testColonist != null && testColonist.Spawned && !testColonist.Dead
                && !testColonist.Drafted
                && !testColonist.Position.InHorDistOf(core.Position, 8f))
            {
                testColonist.Position = core.Position + new IntVec3(3, 0, 0);
                testColonist.pather?.StopDead();
                testColonist.jobs?.StopAll();
            }

            if (!chatMemoryObserved)
            {
                foreach (Pawn colonist in core.Map.mapPawns.FreeColonistsSpawned)
                {
                    if (colonist.needs?.mood?.thoughts?.memories?
                            .GetFirstMemoryOfDef(HoloAI_DefOf.HoloAI_TalkedWithPRISM) != null)
                    {
                        chatMemoryObserved = true;
                        Log.Message("[HoloAI SelfTest] observed chat memory on "
                            + colonist.LabelShort + " at tick " + ticks);
                        break;
                    }
                }
            }

            if (!chatLogObserved)
            {
                foreach (LogEntry entry in Find.PlayLog.AllEntries)
                {
                    if (entry is PlayLogEntry_Interaction
                        && entry.GetConcerns().Contains(core.Avatar))
                    {
                        Pawn recipient = entry.GetConcerns().OfType<Pawn>()
                            .FirstOrDefault(p => p != core.Avatar);
                        string text = entry.ToGameStringFromPOV(recipient ?? (Pawn)core.Avatar);
                        if (!text.NullOrEmpty())
                        {
                            chatLogObserved = true;
                            Log.Message("[HoloAI SelfTest] chat log line: " + text);
                        }
                        break;
                    }
                }
            }
        }

        private void Check(string label, bool pass)
        {
            if (!pass)
            {
                failures++;
            }
            Log.Message("[HoloAI SelfTest] " + (pass ? "PASS" : "FAIL") + ": " + label);
        }

        private void Finish()
        {
            done = true;
            Log.Message("[HoloAI SelfTest] COMPLETE: " + failures + " failures");
        }
    }
}
