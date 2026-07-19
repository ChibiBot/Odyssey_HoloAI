using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

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
                    Check("avatar teleported back to substructure near core",
                        core.Avatar != null && core.Avatar.Spawned
                        && map.terrainGrid.FoundationAt(core.Avatar.Position)?.IsSubstructure == true
                        && core.Avatar.Position.InHorDistOf(core.Position, 6f));
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
                    Finish();
                    break;
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
            testColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
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
                avatar.AttachStyleTrackers();
                _ = new Dialog_HoloStyling(avatar);
                avatar.DetachStyleTrackers();
                Check("styling dialog constructs and trackers detach",
                    avatar.story == null && avatar.style == null);
            }
            catch (System.Exception e)
            {
                Log.Message("[HoloAI SelfTest] styling dialog threw: " + e);
                Check("styling dialog constructs and trackers detach", pass: false);
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

            // Swap back to a second persona to verify ejection of the old matrix.
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
            Check("previous persona matrix ejected on swap", ejected);
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
