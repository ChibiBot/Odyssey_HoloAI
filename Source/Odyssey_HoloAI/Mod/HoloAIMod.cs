using UnityEngine;
using Verse;

namespace Odyssey_HoloAI;

public class HoloAIMod : Mod
{
    private readonly HoloAIModSettings settings;

    public static HoloAIMod Instance { get; private set; } = null!;

    public static HoloAIModSettings Settings => Instance.settings;

    public HoloAIMod(ModContentPack content) : base(content)
    {
        Instance = this;
        settings = GetSettings<HoloAIModSettings>();
        settings.EnsureBuffers();
    }

    public override string SettingsCategory() => "Odyssey.HoloAI.ModTitle".Translate();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        settings.EnsureBuffers();

        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label("Odyssey.HoloAI.Settings.Description".Translate());
        listing.GapLine();

        DrawEditableList(listing,
            "Odyssey.HoloAI.Settings.AllowedTerrains".Translate(),
            ref settings.AllowedTerrainsBuffer,
            "Odyssey.HoloAI.Settings.AllowedTerrainsTooltip".Translate());

        listing.Gap();

        DrawEditableList(listing,
            "Odyssey.HoloAI.Settings.AnchorBuildings".Translate(),
            ref settings.AnchorThingsBuffer,
            "Odyssey.HoloAI.Settings.AnchorBuildingsTooltip".Translate());

        listing.GapLine();
        if (listing.ButtonText("Odyssey.HoloAI.Settings.Reset".Translate()))
        {
            settings.ResetToDefaults();
        }

        listing.End();

        settings.SyncListsFromBuffers();
    }

    private static void DrawEditableList(Listing_Standard listing, string label, ref string buffer, string tooltip)
    {
        listing.Label(label);
        var rect = listing.GetRect(60f);
        Widgets.DrawMenuSection(rect);
        var textRect = rect.ContractedBy(4f);
        buffer = Widgets.TextArea(textRect, buffer);
        if (!tooltip.NullOrEmpty())
        {
            Widgets.DrawHighlightIfMouseover(rect);
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}
