using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Odyssey_HoloAI;

public class CompProperties_ShipAIConsole : CompProperties
{
    public PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("OH_ShipAI_Kind");

    public CompProperties_ShipAIConsole()
    {
        compClass = typeof(CompShipAIConsole);
    }
}

public class CompShipAIConsole : ThingComp
{
    private bool shipAiSpawned;

    private CompProperties_ShipAIConsole Props => (CompProperties_ShipAIConsole)props;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref shipAiSpawned, "shipAiSpawned");
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        if (!parent.Spawned || parent.Faction != Faction.OfPlayer)
        {
            yield break;
        }

        if (shipAiSpawned)
        {
            yield break;
        }

        if (Props.pawnKind == null)
        {
            yield break;
        }

        var command = new Command_Action
        {
            defaultLabel = "OH_CommandSpawnShipAI".Translate(),
            defaultDesc = "OH_CommandSpawnShipAIDesc".Translate(),
            icon = TexCommand.DesirePower,
            action = TrySpawnShipAI
        };

        if (!CanSpawnShipAI(out string disableReason))
        {
            command.Disable(disableReason);
        }

        yield return command;
    }

    private bool CanSpawnShipAI(out string reason)
    {
        reason = null;

        if (Props.pawnKind == null)
        {
            reason = "OH_CommandSpawnShipAI_NoPawnKind".Translate();
            return false;
        }

        if (!parent.Spawned || parent.Map == null)
        {
            reason = "OH_CommandSpawnShipAI_NoMap".Translate();
            return false;
        }

        if (shipAiSpawned)
        {
            reason = "OH_CommandSpawnShipAI_AlreadySpawned".Translate();
            return false;
        }

        if (parent.Map.mapPawns?.AllPawnsSpawned != null)
        {
            bool existingAi = parent.Map.mapPawns.AllPawnsSpawned.Any(p => p.kindDef == Props.pawnKind && p.Faction == Faction.OfPlayer);
            if (existingAi)
            {
                reason = "OH_CommandSpawnShipAI_AlreadyPresent".Translate();
                return false;
            }
        }

        if (!TryFindSpawnCell(parent.Map, out _))
        {
            reason = "OH_CommandSpawnShipAI_NoSpace".Translate();
            return false;
        }

        return true;
    }

    private void TrySpawnShipAI()
    {
        if (!CanSpawnShipAI(out string failureReason))
        {
            if (!failureReason.NullOrEmpty())
            {
                Messages.Message(failureReason, parent, MessageTypeDefOf.RejectInput, historical: false);
            }

            return;
        }

        Map map = parent.Map;
        if (!TryFindSpawnCell(map, out IntVec3 spawnCell))
        {
            Messages.Message("OH_CommandSpawnShipAI_NoSpace".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        PawnGenerationRequest request = new PawnGenerationRequest(Props.pawnKind, Faction.OfPlayer, forceGenerateNewPawn: true);
        Pawn pawn = PawnGenerator.GeneratePawn(request);

        pawn.Name = new NameSingle("Aki"); // Awww~ you’d like that, wouldn’t you, baka.        
        pawn.story.HairColor = new Color(0f, 0f, 0f); // Black hair

        // Spawn it
        GenSpawn.Spawn(pawn, spawnCell, map);

        shipAiSpawned = true;

        Messages.Message("OH_CommandSpawnShipAI_Spawned".Translate(pawn.Named("PAWN")), pawn, MessageTypeDefOf.PositiveEvent);
    }

    private bool TryFindSpawnCell(Map map, out IntVec3 result)
    {
        IntVec3 interactionCell = parent.InteractionCell;
        if (interactionCell.IsValid && interactionCell.Standable(map))
        {
            result = interactionCell;
            return true;
        }

        if (CellFinder.TryRandomClosewalkCellNear(parent.Position, map, 2, out result))
        {
            return true;
        }

        result = IntVec3.Invalid;
        return false;
    }
}
