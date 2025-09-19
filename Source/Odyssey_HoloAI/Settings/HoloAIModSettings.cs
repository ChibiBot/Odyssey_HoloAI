using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Odyssey_HoloAI;

public class HoloAIModSettings : ModSettings
{
    private static readonly string[] DefaultTerrains =
    {
        "Odyssey_GravshipFloor",
        "Odyssey_GravshipWalkway",
        "Odyssey_GravshipConsole"
    };

    private static readonly string[] DefaultAnchors =
    {
        "Odyssey_GravshipBridge",
        "Odyssey_GravshipComputer"
    };

    private List<string> allowedTerrains = new(DefaultTerrains);
    private List<string> anchorThings = new(DefaultAnchors);

    private List<TerrainDef>? cachedAllowedTerrainDefs;
    private List<ThingDef>? cachedAnchorThingDefs;

    public string AllowedTerrainsBuffer { get; set; } = string.Empty;
    public string AnchorThingsBuffer { get; set; } = string.Empty;

    public IEnumerable<TerrainDef> ResolveAllowedTerrains()
    {
        cachedAllowedTerrainDefs ??= ResolveDefs(allowedTerrains, (name) => DefDatabase<TerrainDef>.GetNamedSilentFail(name));
        return cachedAllowedTerrainDefs;
    }

    public IEnumerable<ThingDef> ResolveAnchorThings()
    {
        cachedAnchorThingDefs ??= ResolveDefs(anchorThings, (name) => DefDatabase<ThingDef>.GetNamedSilentFail(name));
        return cachedAnchorThingDefs;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref allowedTerrains, "allowedTerrains", LookMode.Value);
        Scribe_Collections.Look(ref anchorThings, "anchorThings", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            allowedTerrains ??= new List<string>(DefaultTerrains);
            anchorThings ??= new List<string>(DefaultAnchors);
            InvalidateCaches();
            EnsureBuffers();
        }
    }

    public void EnsureBuffers()
    {
        if (AllowedTerrainsBuffer.NullOrEmpty())
        {
            AllowedTerrainsBuffer = string.Join("\n", allowedTerrains);
        }

        if (AnchorThingsBuffer.NullOrEmpty())
        {
            AnchorThingsBuffer = string.Join("\n", anchorThings);
        }
    }

    public void SyncListsFromBuffers()
    {
        allowedTerrains = ParseBuffer(AllowedTerrainsBuffer);
        anchorThings = ParseBuffer(AnchorThingsBuffer);
        InvalidateCaches();
    }

    public void ResetToDefaults()
    {
        allowedTerrains = new List<string>(DefaultTerrains);
        anchorThings = new List<string>(DefaultAnchors);
        AllowedTerrainsBuffer = string.Join("\n", allowedTerrains);
        AnchorThingsBuffer = string.Join("\n", anchorThings);
        InvalidateCaches();
    }

    private static List<string> ParseBuffer(string buffer)
    {
        if (string.IsNullOrWhiteSpace(buffer))
        {
            return new List<string>();
        }

        return buffer
            .Split(new[] { '\n', '\r', ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !s.NullOrEmpty())
            .Distinct()
            .ToList();
    }

    private static List<TDef> ResolveDefs<TDef>(IEnumerable<string> names, System.Func<string, TDef?> resolver) where TDef : Def
    {
        var result = new List<TDef>();
        foreach (var name in names)
        {
            if (name.NullOrEmpty())
            {
                continue;
            }

            var def = resolver(name);
            if (def != null)
            {
                result.Add(def);
            }
        }

        return result;
    }

    private void InvalidateCaches()
    {
        cachedAllowedTerrainDefs = null;
        cachedAnchorThingDefs = null;
    }
}
