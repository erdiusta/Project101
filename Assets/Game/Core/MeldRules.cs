using System.Collections.Generic;

namespace Project101.Core
{
    public enum MeldKind : byte
    {
        Run,
        Set,
        Pair
    }

    public readonly struct MeldInfo
    {
        public readonly MeldKind Kind;

        /// <summary>Points the meld counts towards opening. Pairs count none.</summary>
        public readonly int Points;

        public MeldInfo(MeldKind kind, int points)
        {
            Kind = kind;
            Points = points;
        }
    }

    public static class MeldRules
    {
        /// <summary>
        /// Checks a group of tiles as the player arranged it. Runs are read in the given order.
        /// A group that is valid both as a run and as a set counts as whichever scores more.
        /// </summary>
        public static bool TryEvaluate(IReadOnlyList<Tile> tiles, Okey okey, RuleSet rules, out MeldInfo info)
        {
            info = default;
            var faces = new Face[tiles.Count];
            for (int i = 0; i < faces.Length; i++) faces[i] = okey.Resolve(tiles[i]);

            if (faces.Length == 2)
            {
                if (!IsPair(faces[0], faces[1])) return false;
                info = new MeldInfo(MeldKind.Pair, 0);
                return true;
            }

            bool isRun = TryRun(faces, rules.WrapRuns, out int runPoints);
            bool isSet = TrySet(faces, out int setPoints);
            if (!isRun && !isSet) return false;

            info = isRun && (!isSet || runPoints >= setPoints)
                ? new MeldInfo(MeldKind.Run, runPoints)
                : new MeldInfo(MeldKind.Set, setPoints);
            return true;
        }

        /// <summary>
        /// Puts a loose group of tiles into a valid meld order. The given order wins when it is
        /// already valid; otherwise the tiles are sorted and the wilds tried in every gap.
        /// </summary>
        public static bool TryArrange(IReadOnlyList<Tile> tiles, Okey okey, RuleSet rules, out List<Tile> arranged, out MeldInfo info)
        {
            arranged = new List<Tile>(tiles);
            if (TryEvaluate(arranged, okey, rules, out info)) return true;

            var wilds = new List<Tile>();
            var rest = new List<Tile>();
            foreach (var tile in tiles) (okey.IsWild(tile) ? wilds : rest).Add(tile);
            rest.Sort((a, b) => okey.Resolve(a).Number.CompareTo(okey.Resolve(b).Number));

            List<Tile> best = null;
            var bestInfo = default(MeldInfo);
            TryWildPlacements(rest, wilds, 0, okey, rules, ref best, ref bestInfo);

            // With wrapping runs a leading 1 may belong after the 13.
            if (rules.WrapRuns && rest.Count > 0 && okey.Resolve(rest[0]).Number == 1)
            {
                rest.Add(rest[0]);
                rest.RemoveAt(0);
                TryWildPlacements(rest, wilds, 0, okey, rules, ref best, ref bestInfo);
            }

            if (best == null) return false;
            arranged = best;
            info = bestInfo;
            return true;
        }

        static void TryWildPlacements(List<Tile> order, List<Tile> wilds, int next, Okey okey, RuleSet rules,
            ref List<Tile> best, ref MeldInfo bestInfo)
        {
            if (next == wilds.Count)
            {
                if (TryEvaluate(order, okey, rules, out var info) && (best == null || info.Points > bestInfo.Points))
                {
                    best = new List<Tile>(order);
                    bestInfo = info;
                }
                return;
            }

            for (int i = 0; i <= order.Count; i++)
            {
                order.Insert(i, wilds[next]);
                TryWildPlacements(order, wilds, next + 1, okey, rules, ref best, ref bestInfo);
                order.RemoveAt(i);
            }
        }

        static bool IsPair(Face a, Face b) =>
            a.IsWild || b.IsWild || (a.Color == b.Color && a.Number == b.Number);

        static bool TryRun(Face[] faces, bool wrap, out int points)
        {
            points = 0;
            if (faces.Length < 3) return false;

            int anchor = System.Array.FindIndex(faces, f => !f.IsWild);
            if (anchor < 0) return false;

            if (RunFrom(faces, anchor, faces[anchor].Number - anchor, wrap, out points)) return true;
            // A 1 can also close a run after 13.
            return wrap && faces[anchor].Number == 1 && RunFrom(faces, anchor, 14 - anchor, wrap, out points);
        }

        static bool RunFrom(Face[] faces, int anchor, int start, bool wrap, out int points)
        {
            points = 0;
            if (start < 1 || start + faces.Length - 1 > (wrap ? 14 : 13)) return false;

            for (int i = 0; i < faces.Length; i++)
            {
                int number = start + i == 14 ? 1 : start + i;
                if (!faces[i].IsWild && (faces[i].Color != faces[anchor].Color || faces[i].Number != number)) return false;
                points += number;
            }
            return true;
        }

        static bool TrySet(Face[] faces, out int points)
        {
            points = 0;
            if (faces.Length < 3 || faces.Length > 4) return false;

            int number = 0;
            int colors = 0;
            foreach (var face in faces)
            {
                if (face.IsWild) continue;
                if (number != 0 && face.Number != number) return false;
                number = face.Number;

                int bit = 1 << (int)face.Color;
                if ((colors & bit) != 0) return false;
                colors |= bit;
            }
            if (number == 0) return false;

            points = number * faces.Length;
            return true;
        }
    }
}
