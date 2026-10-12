using System.Collections.Generic;

namespace Project101.Core
{
    /// <summary>Finds melds in a rack. Used by the bots; it never spends a wild tile.</summary>
    public static class MeldFinder
    {
        /// <summary>Non-overlapping runs and sets, picked greedily in whichever order scores more.</summary>
        public static List<List<Tile>> FindMelds(IReadOnlyList<Tile> rack, Okey okey)
        {
            var runsFirst = Greedy(rack, okey, true);
            var setsFirst = Greedy(rack, okey, false);
            return Points(runsFirst, okey) >= Points(setsFirst, okey) ? runsFirst : setsFirst;
        }

        public static List<List<Tile>> FindPairs(IReadOnlyList<Tile> rack, Okey okey)
        {
            var pairs = new List<List<Tile>>();
            var first = new Dictionary<int, Tile>();
            foreach (var tile in rack)
            {
                if (okey.IsWild(tile)) continue;
                var face = okey.Resolve(tile);
                int key = (int)face.Color * 13 + face.Number;
                if (first.TryGetValue(key, out var other))
                {
                    pairs.Add(new List<Tile> { other, tile });
                    first.Remove(key);
                }
                else
                {
                    first.Add(key, tile);
                }
            }
            return pairs;
        }

        /// <summary>Opening points of runs and sets: every tile counts its number.</summary>
        public static int Points(List<List<Tile>> melds, Okey okey)
        {
            int points = 0;
            foreach (var meld in melds)
            foreach (var tile in meld)
                points += okey.Resolve(tile).Number;
            return points;
        }

        static List<List<Tile>> Greedy(IReadOnlyList<Tile> rack, Okey okey, bool runsFirst)
        {
            var pool = new List<Tile>();
            foreach (var tile in rack)
            {
                if (!okey.IsWild(tile)) pool.Add(tile);
            }

            var melds = new List<List<Tile>>();
            if (!runsFirst) TakeSets(pool, okey, melds);
            // Twice, because the second copies of a color can form a run of their own.
            TakeRuns(pool, okey, melds);
            TakeRuns(pool, okey, melds);
            if (runsFirst) TakeSets(pool, okey, melds);
            return melds;
        }

        static void TakeRuns(List<Tile> pool, Okey okey, List<List<Tile>> melds)
        {
            for (int color = 0; color < 4; color++)
            {
                var byNumber = new Tile?[15];
                foreach (var tile in pool)
                {
                    var face = okey.Resolve(tile);
                    if ((int)face.Color == color && byNumber[face.Number] == null) byNumber[face.Number] = tile;
                }

                var run = new List<Tile>();
                for (int number = 1; number <= 14; number++)
                {
                    if (byNumber[number] != null)
                    {
                        run.Add(byNumber[number].Value);
                        continue;
                    }
                    if (run.Count >= 3) Take(pool, run, melds);
                    run = new List<Tile>();
                }
            }
        }

        static void TakeSets(List<Tile> pool, Okey okey, List<List<Tile>> melds)
        {
            for (int number = 1; number <= 13; number++)
            {
                var byColor = new Tile?[4];
                var set = new List<Tile>();
                foreach (var tile in pool)
                {
                    var face = okey.Resolve(tile);
                    if (face.Number != number || byColor[(int)face.Color] != null) continue;
                    byColor[(int)face.Color] = tile;
                    set.Add(tile);
                }
                if (set.Count >= 3) Take(pool, set, melds);
            }
        }

        static void Take(List<Tile> pool, List<Tile> meld, List<List<Tile>> melds)
        {
            foreach (var tile in meld) pool.Remove(tile);
            melds.Add(meld);
        }
    }
}
