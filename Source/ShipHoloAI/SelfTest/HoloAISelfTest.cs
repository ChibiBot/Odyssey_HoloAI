using System;
using System.Linq;
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
                    GenSpawn.Spawn(core, map.Center + new IntVec3(4, 0, 4), map);
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
                case 9200:
                    Check("letter triggered a P.R.I.S.M. announcement",
                        PrismSpeech.LastSpokenTick >= ticks - 200 && !PrismSpeech.LastLine.NullOrEmpty());
                    if (!PrismSpeech.LastLine.NullOrEmpty())
                    {
                        Log.Message("[HoloAI SelfTest] announcement line: " + PrismSpeech.LastLine);
                    }
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
            IntVec3 center = map.Center;
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
            Thing coreThing = ThingMaker.MakeThing(HoloAI_DefOf.HoloAI_HoloCore);
            coreThing.SetFaction(Faction.OfPlayer);
            core = (Building_HoloCore)GenSpawn.Spawn(coreThing, center, map);
            testColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            GenSpawn.Spawn(testColonist, center + new IntVec3(3, 0, 0), map);
            Log.Message("[HoloAI SelfTest] setup complete at " + center);
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
            // The colonist AI may walk them off the test patch; anchor them near the core
            // so the avatar gets her chance to chat.
            if (testColonist != null && testColonist.Spawned && !testColonist.Dead
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
