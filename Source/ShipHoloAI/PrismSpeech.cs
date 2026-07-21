using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace ShipHoloAI
{
    /// <summary>
    /// The avatar's voice: resolves lines from the announcements rule pack and
    /// delivers them as overhead barks plus prefixed side messages. Lines are
    /// persona-keyed — a persona with speechPrefix "ixia" resolves
    /// "ixia_announce_lowfuel" when such lines exist and falls back to the base
    /// "announce_lowfuel" (P.R.I.S.M.'s voice) when they don't, so a new persona
    /// works with zero lines written and gains its own voice as lines are added.
    /// Overhead text is tinted toward the persona's hair color, so who is
    /// speaking reads at a glance.
    /// </summary>
    public static class PrismSpeech
    {
        private static readonly Color DefaultSpeechColor = new Color(0f, 0.855f, 1f);

        private const int MinTicksBetweenLines = 300;

        public static int LastSpokenTick = -99999;
        public static string LastLine;

        private static HashSet<string> keywords;

        private static HashSet<string> Keywords
        {
            get
            {
                if (keywords == null)
                {
                    keywords = new HashSet<string>();
                    foreach (Rule rule in HoloAI_DefOf.HoloAI_Announcements.RulesPlusIncludes)
                    {
                        keywords.Add(rule.keyword);
                    }
                }
                return keywords;
            }
        }

        public static bool HasKeyword(string keyword)
        {
            return Keywords.Contains(keyword);
        }

        public static Building_HoloCore FindActiveCore(Map map)
        {
            if (map == null)
            {
                return null;
            }
            foreach (Building_HoloCore building in map.listerBuildings.AllBuildingsColonistOfClass<Building_HoloCore>())
            {
                if (building.Powered)
                {
                    return building;
                }
            }
            return null;
        }

        public static string ResolveLine(string rootKeyword)
        {
            // The keyword check keeps a missing root from reaching GrammarResolver,
            // which would log a resolution error.
            if (!HasKeyword(rootKeyword))
            {
                return null;
            }
            GrammarRequest request = default;
            request.Includes.Add(HoloAI_DefOf.HoloAI_Announcements);
            return GrammarResolver.Resolve(rootKeyword, request, null, forceLog: false);
        }

        /// <summary>Persona-keyed resolve: "&lt;prefix&gt;_&lt;root&gt;" when the
        /// persona declares a speechPrefix and lines exist for it, base root
        /// (P.R.I.S.M.'s voice) otherwise.</summary>
        public static string ResolveLineFor(HoloPersonaDef persona, string rootKeyword)
        {
            string prefix = persona?.speechPrefix;
            if (!prefix.NullOrEmpty() && HasKeyword(prefix + "_" + rootKeyword))
            {
                return ResolveLine(prefix + "_" + rootKeyword);
            }
            return ResolveLine(rootKeyword);
        }

        /// <summary>Overhead-text tint: the persona's hair color lifted toward
        /// white, so dark palettes (I.X.I.A.'s crimson) stay readable over dark
        /// terrain.</summary>
        public static Color SpeechColorFor(HoloPersonaDef persona)
        {
            return persona == null
                ? DefaultSpeechColor
                : Color.Lerp(persona.hairColor, Color.white, 0.35f);
        }

        /// <summary>One overhead line in the avatar's persona voice — resolve,
        /// tint, throw. The shared path for every job driver that has her speak.</summary>
        public static void Bark(Pawn avatar, string rootKeyword)
        {
            if (avatar == null || !avatar.Spawned)
            {
                return;
            }
            HoloPersonaDef persona = (avatar as Pawn_HoloAvatar)?.holoCore?.ActivePersona;
            string line = ResolveLineFor(persona, rootKeyword);
            if (!line.NullOrEmpty())
            {
                MoteMaker.ThrowText(avatar.DrawPos + new Vector3(0f, 0f, 0.65f),
                    avatar.Map, line, SpeechColorFor(persona), 5f);
            }
        }

        public static void Say(Map map, string rootKeyword)
        {
            Building_HoloCore core = FindActiveCore(map);
            if (core == null || Find.TickManager.TicksGame - LastSpokenTick < MinTicksBetweenLines)
            {
                return;
            }
            HoloPersonaDef persona = core.ActivePersona;
            string line = ResolveLineFor(persona, rootKeyword);
            if (line.NullOrEmpty())
            {
                return;
            }
            LastSpokenTick = Find.TickManager.TicksGame;
            LastLine = line;

            Pawn_HoloAvatar avatar = core.Avatar;
            if (avatar != null && avatar.Spawned)
            {
                MoteMaker.ThrowText(avatar.DrawPos + new Vector3(0f, 0f, 0.65f),
                    map, line, SpeechColorFor(persona), 6f);
            }
            string speaker = persona?.avatarName ?? "P.R.I.S.M.";
            Messages.Message(speaker + ": " + line, new LookTargets(core),
                MessageTypeDefOf.SilentInput, historical: false);
        }
    }
}
