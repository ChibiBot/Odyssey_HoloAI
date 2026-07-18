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

        public HairDef DefaultHair =>
            DefDatabase<HairDef>.GetNamedSilentFail(defaultHairDefName);
    }

    /// <summary>Links a matrix item ThingDef back to its persona.</summary>
    public class HoloPersonaMatrixExtension : DefModExtension
    {
        public HoloPersonaDef persona;
    }
}
