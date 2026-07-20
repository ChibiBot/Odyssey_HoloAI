using RimWorld;
using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>
    /// A holocore-resident persona: identity (name, default hair/color) plus an
    /// optional shipboard aura hediff applied to crew on substructure while the
    /// avatar is projected. The default P.R.I.S.M. persona has no matrix item;
    /// purchased personas arrive as matrix items and are installed into the core.
    /// </summary>
    public class HoloPersonaDef : Def
    {
        public string avatarName;
        public Color hairColor = Pawn_HoloAvatar.DefaultHairColor;
        public string defaultHairDefName = "Flowy";
        public HediffDef auraHediff;
        public ThingDef matrixItem;

        // Selection-screen lore (Dialog_PersonaSwitcher): who built her, the
        // catalog blurb about them, and her own personality-forward bio.
        public string manufacturer;
        public string manufacturerStory;
        public string bio;

        /// <summary>
        /// True for personas whose work is done by the avatar herself rather than
        /// spread to nearby crew (e.g. a warden persona performing prisoner/slave
        /// interactions in person). When set, HoloAuraMapComponent applies/refreshes
        /// auraHediff on the avatar's own health.hediffSet instead of on colonists
        /// standing on substructure.
        /// </summary>
        public bool auraTargetsAvatar;

        public HairDef DefaultHair =>
            DefDatabase<HairDef>.GetNamedSilentFail(defaultHairDefName);
    }

    /// <summary>Links a matrix item ThingDef back to its persona.</summary>
    public class HoloPersonaMatrixExtension : DefModExtension
    {
        public HoloPersonaDef persona;
    }
}
