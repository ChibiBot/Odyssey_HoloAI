using UnityEngine;
using Verse;

namespace ShipHoloAI
{
    /// <summary>Gizmo icon textures (hand-made art; full-res originals live in
    /// Concept/UI_Icons). Textures must be fetched on the main thread at startup,
    /// hence the StaticConstructorOnStartup pattern.</summary>
    [StaticConstructorOnStartup]
    public static class HoloAIIcons
    {
        public static readonly Texture2D SwitchPersona =
            ContentFinder<Texture2D>.Get("HoloAI/UI/SwitchPersona");
        public static readonly Texture2D ToggleProjection =
            ContentFinder<Texture2D>.Get("HoloAI/UI/ToggleProjection");
        public static readonly Texture2D Summon =
            ContentFinder<Texture2D>.Get("HoloAI/UI/Summon");
        public static readonly Texture2D Hairstyle =
            ContentFinder<Texture2D>.Get("HoloAI/UI/Hairstyle");
        public static readonly Texture2D Restyle =
            ContentFinder<Texture2D>.Get("HoloAI/UI/Restyle");
    }
}
