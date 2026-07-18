using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace ShipHoloAI
{
    /// <summary>
    /// P.R.I.S.M.'s voice: resolves a line from the announcements rule pack and delivers
    /// it as an overhead bark on the avatar plus a prefixed side message.
    /// </summary>
    public static class PrismSpeech
    {
        private static readonly Color SpeechColor = new Color(0f, 0.855f, 1f);

        private const int MinTicksBetweenLines = 300;

        public static int LastSpokenTick = -99999;
        public static string LastLine;

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
            GrammarRequest request = default;
            request.Includes.Add(HoloAI_DefOf.HoloAI_Announcements);
            return GrammarResolver.Resolve(rootKeyword, request, null, forceLog: false);
        }

        public static void Say(Map map, string rootKeyword)
        {
            Building_HoloCore core = FindActiveCore(map);
            if (core == null || Find.TickManager.TicksGame - LastSpokenTick < MinTicksBetweenLines)
            {
                return;
            }
            string line = ResolveLine(rootKeyword);
            if (line.NullOrEmpty())
            {
                return;
            }
            LastSpokenTick = Find.TickManager.TicksGame;
            LastLine = line;

            Pawn_HoloAvatar avatar = core.Avatar;
            if (avatar != null && avatar.Spawned)
            {
                MoteMaker.ThrowText(avatar.DrawPos + new Vector3(0f, 0f, 0.65f), map, line, SpeechColor, 6f);
            }
            Messages.Message("P.R.I.S.M.: " + line, new LookTargets(core),
                MessageTypeDefOf.SilentInput, historical: false);
        }
    }
}
