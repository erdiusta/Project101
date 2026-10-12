using System.Collections.Generic;

namespace Project101.Core
{
    /// <summary>
    /// A basic computer player. It always draws from the stock, opens as soon as it can,
    /// gets rid of whatever fits the table, and throws away its least connected tile.
    /// </summary>
    public static class Bot
    {
        public static void PlayTurn(Round round, int player)
        {
            if (round.Phase == TurnPhase.Draw) round.DrawFromStock(player);
            if (round.Phase != TurnPhase.Play || round.Current != player) return;

            if (round.Opened(player) == OpenKind.None) TryOpen(round, player);
            if (round.Opened(player) != OpenKind.None)
            {
                LayMelds(round, player);
                AddToTable(round, player);
            }
            round.Discard(player, ChooseDiscard(round, player));
        }

        static void TryOpen(Round round, int player)
        {
            var rack = round.Rack(player);

            var melds = MeldFinder.FindMelds(rack, round.Okey);
            KeepADiscard(melds, rack.Count);
            if (melds.Count > 0 && MeldFinder.Points(melds, round.Okey) >= round.RequiredPoints
                && round.Open(player, melds) == MoveResult.Ok) return;

            var pairs = MeldFinder.FindPairs(rack, round.Okey);
            KeepADiscard(pairs, rack.Count);
            if (pairs.Count >= round.RequiredPairs) round.Open(player, pairs);
        }

        // A hand made entirely of melds still needs one tile to discard.
        static void KeepADiscard(List<List<Tile>> melds, int rackCount)
        {
            int used = 0;
            foreach (var meld in melds) used += meld.Count;
            if (used >= rackCount && melds.Count > 0) melds.RemoveAt(melds.Count - 1);
        }

        static void LayMelds(Round round, int player)
        {
            var melds = round.Opened(player) == OpenKind.Pairs
                ? MeldFinder.FindPairs(round.Rack(player), round.Okey)
                : MeldFinder.FindMelds(round.Rack(player), round.Okey);
            foreach (var meld in melds) round.Lay(player, meld);
        }

        static void AddToTable(Round round, int player)
        {
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var tile in new List<Tile>(round.Rack(player)))
                {
                    for (int meld = 0; meld < round.Table.Count && !added; meld++)
                    {
                        added = round.AddToMeld(player, meld, tile, false) == MoveResult.Ok
                                || round.AddToMeld(player, meld, tile, true) == MoveResult.Ok;
                    }
                    if (added) break;
                }
            }
        }

        static Tile ChooseDiscard(Round round, int player)
        {
            var rack = round.Rack(player);
            bool opened = round.Opened(player) != OpenKind.None;

            var best = rack[0];
            int bestCost = int.MaxValue;
            foreach (var tile in rack)
            {
                int value = round.Okey.HandValue(tile);
                // Once open, shed points; before that, keep whatever might still become a meld.
                int cost = opened ? -value : Connections(rack, tile, round.Okey) * 100 - value;
                if (round.Okey.IsWild(tile) || round.FitsAnyMeld(tile)) cost += 100000;
                if (cost < bestCost)
                {
                    best = tile;
                    bestCost = cost;
                }
            }
            return best;
        }

        static int Connections(IReadOnlyList<Tile> rack, Tile tile, Okey okey)
        {
            var face = okey.Resolve(tile);
            int count = 0;
            foreach (var other in rack)
            {
                if (other == tile) continue;
                var otherFace = okey.Resolve(other);
                int gap = System.Math.Abs(otherFace.Number - face.Number);
                if (otherFace.Color == face.Color ? gap <= 2 : gap == 0) count++;
            }
            return count;
        }
    }
}
