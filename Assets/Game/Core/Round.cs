using System;
using System.Collections.Generic;

namespace Project101.Core
{
    public enum OpenKind : byte
    {
        None,
        Melds,
        Pairs
    }

    public enum TurnPhase : byte
    {
        Draw,
        Play,
        Finished
    }

    public enum MoveResult
    {
        Ok,
        NotYourTurn,
        WrongPhase,
        TileNotInRack,
        InvalidMeld,
        MixedOpening,
        OpeningTooLow,
        AlreadyOpened,
        NotOpened,
        WrongMeldKind,
        BadMeldIndex,
        NoDiscardToTake,
        MustDrawFromStock,
        MustUseTakenTile,
        MustKeepDiscard,
        NothingToReturn
    }

    public sealed class TableMeld
    {
        public readonly int Owner;
        public MeldKind Kind { get; internal set; }
        public readonly List<Tile> Tiles;

        internal TableMeld(int owner, MeldKind kind, List<Tile> tiles)
        {
            Owner = owner;
            Kind = kind;
            Tiles = tiles;
        }
    }

    /// <summary>
    /// One hand of 101, from the deal to the last discard. This is the authoritative state:
    /// it holds every rack, so it must only ever live on the host or server.
    /// </summary>
    public sealed class Round
    {
        public const int Players = 4;
        const int RackSize = 21;

        public readonly RuleSet Rules;
        public Tile Indicator { get; private set; }
        public Okey Okey { get; private set; }

        public int Current { get; private set; }
        public TurnPhase Phase { get; private set; }

        /// <summary>Seat that finished the round, or -1 while playing or when the stock ran out.</summary>
        public int Winner { get; private set; } = -1;
        public bool FinishedWithOkey { get; private set; }

        /// <summary>What the next opening has to reach. Only moves when the table is progressive.</summary>
        public int RequiredPoints { get; private set; }
        public int RequiredPairs { get; private set; }

        internal readonly List<Tile>[] Racks = new List<Tile>[Players];
        internal readonly List<Tile>[] Discards = new List<Tile>[Players];
        internal readonly List<Tile> Stock = new List<Tile>(Tile.Count);
        readonly List<TableMeld> table = new List<TableMeld>();
        readonly OpenKind[] opened = new OpenKind[Players];
        readonly int[] penalties = new int[Players];

        // The tile taken from the previous player's discards this turn, until it is melded.
        Tile? taken;
        bool mustDrawFromStock;

        public IReadOnlyList<TableMeld> Table => table;
        public int StockCount => Stock.Count;
        public bool HoldsTakenDiscard => taken != null;
        public OpenKind Opened(int player) => opened[player];
        public int Penalty(int player) => penalties[player];
        public IReadOnlyList<Tile> Rack(int player) => Racks[player];
        public IReadOnlyList<Tile> DiscardPile(int player) => Discards[player];

        public Round(RuleSet rules, int seed, int starter = 0)
        {
            Rules = rules;
            RequiredPoints = rules.OpeningPoints;
            RequiredPairs = rules.OpeningPairs;

            for (int i = 0; i < Tile.Count; i++) Stock.Add(new Tile(i));
            var random = new Random(seed);
            for (int i = Stock.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Stock[i], Stock[j]) = (Stock[j], Stock[i]);
            }

            int indicatorIndex = Stock.FindLastIndex(t => !t.IsFalseJoker);
            SetIndicator(Stock[indicatorIndex]);
            Stock.RemoveAt(indicatorIndex);

            for (int p = 0; p < Players; p++)
            {
                Racks[p] = new List<Tile>(RackSize + 1);
                Discards[p] = new List<Tile>();
                Deal(p, RackSize);
            }

            // The starter holds the extra tile and begins by discarding.
            Deal(starter, 1);
            Current = starter;
            Phase = TurnPhase.Play;
        }

        internal void SetIndicator(Tile indicator)
        {
            Indicator = indicator;
            Okey = Okey.FromIndicator(indicator);
        }

        void Deal(int player, int count)
        {
            Racks[player].AddRange(Stock.GetRange(Stock.Count - count, count));
            Stock.RemoveRange(Stock.Count - count, count);
        }

        public MoveResult DrawFromStock(int player)
        {
            var check = CheckTurn(player, TurnPhase.Draw);
            if (check != MoveResult.Ok) return check;

            Racks[player].Add(Stock[Stock.Count - 1]);
            Stock.RemoveAt(Stock.Count - 1);
            mustDrawFromStock = false;
            Phase = TurnPhase.Play;
            return MoveResult.Ok;
        }

        /// <summary>Takes the previous player's last discard. It has to be melded before the turn can end.</summary>
        public MoveResult TakeDiscard(int player)
        {
            var check = CheckTurn(player, TurnPhase.Draw);
            if (check != MoveResult.Ok) return check;
            if (mustDrawFromStock) return MoveResult.MustDrawFromStock;

            var pile = Discards[Previous(player)];
            if (pile.Count == 0) return MoveResult.NoDiscardToTake;

            taken = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            Racks[player].Add(taken.Value);
            Phase = TurnPhase.Play;
            return MoveResult.Ok;
        }

        /// <summary>Puts an unused taken discard back. The player then has to draw from the stock.</summary>
        public MoveResult ReturnTakenDiscard(int player)
        {
            var check = CheckTurn(player, TurnPhase.Play);
            if (check != MoveResult.Ok) return check;
            if (taken == null) return MoveResult.NothingToReturn;

            Racks[player].Remove(taken.Value);
            Discards[Previous(player)].Add(taken.Value);
            taken = null;
            mustDrawFromStock = true;
            Phase = TurnPhase.Draw;
            return MoveResult.Ok;
        }

        /// <summary>Lays down the first melds: runs and sets worth the required points, or the required number of pairs.</summary>
        public MoveResult Open(int player, IReadOnlyList<IReadOnlyList<Tile>> melds)
        {
            var check = CheckTurn(player, TurnPhase.Play);
            if (check != MoveResult.Ok) return check;
            if (opened[player] != OpenKind.None) return MoveResult.AlreadyOpened;
            if (melds.Count == 0) return MoveResult.InvalidMeld;

            var all = new List<Tile>();
            foreach (var meld in melds) all.AddRange(meld);
            if (!HasAll(player, all)) return MoveResult.TileNotInRack;
            if (all.Count >= Racks[player].Count) return MoveResult.MustKeepDiscard;

            var infos = new MeldInfo[melds.Count];
            int points = 0;
            int pairs = 0;
            for (int i = 0; i < melds.Count; i++)
            {
                if (!MeldRules.TryEvaluate(melds[i], Okey, Rules, out infos[i])) return MoveResult.InvalidMeld;
                points += infos[i].Points;
                if (infos[i].Kind == MeldKind.Pair) pairs++;
            }

            if (pairs != 0 && pairs != melds.Count) return MoveResult.MixedOpening;
            bool withPairs = pairs != 0;
            if (withPairs ? pairs < RequiredPairs : points < RequiredPoints) return MoveResult.OpeningTooLow;

            for (int i = 0; i < melds.Count; i++) PutOnTable(player, melds[i], infos[i].Kind);
            opened[player] = withPairs ? OpenKind.Pairs : OpenKind.Melds;

            if (Rules.Progressive)
            {
                if (withPairs) RequiredPairs = pairs + 1;
                else RequiredPoints = points + 1;
            }
            return MoveResult.Ok;
        }

        /// <summary>Lays down another meld after opening. Pair openers may only add pairs, the others only runs and sets.</summary>
        public MoveResult Lay(int player, IReadOnlyList<Tile> meld)
        {
            var check = CheckTurn(player, TurnPhase.Play);
            if (check != MoveResult.Ok) return check;
            if (opened[player] == OpenKind.None) return MoveResult.NotOpened;
            if (!HasAll(player, meld)) return MoveResult.TileNotInRack;
            if (meld.Count >= Racks[player].Count) return MoveResult.MustKeepDiscard;
            if (!MeldRules.TryEvaluate(meld, Okey, Rules, out var info)) return MoveResult.InvalidMeld;
            if ((info.Kind == MeldKind.Pair) != (opened[player] == OpenKind.Pairs)) return MoveResult.WrongMeldKind;

            PutOnTable(player, meld, info.Kind);
            return MoveResult.Ok;
        }

        /// <summary>Adds a rack tile to either end of a run or set already on the table.</summary>
        public MoveResult AddToMeld(int player, int meldIndex, Tile tile, bool atFront)
        {
            var check = CheckTurn(player, TurnPhase.Play);
            if (check != MoveResult.Ok) return check;
            if (opened[player] == OpenKind.None) return MoveResult.NotOpened;
            if ((uint)meldIndex >= (uint)table.Count) return MoveResult.BadMeldIndex;
            if (!Racks[player].Contains(tile)) return MoveResult.TileNotInRack;

            if (Racks[player].Count < 2) return MoveResult.MustKeepDiscard;

            var meld = table[meldIndex];
            if (!Fits(meld, tile, atFront, out var kind)) return MoveResult.InvalidMeld;

            Racks[player].Remove(tile);
            meld.Tiles.Insert(atFront ? 0 : meld.Tiles.Count, tile);
            meld.Kind = kind;
            if (taken == tile) taken = null;
            return MoveResult.Ok;
        }

        /// <summary>Ends the turn. Discarding the last tile wins the round.</summary>
        public MoveResult Discard(int player, Tile tile)
        {
            var check = CheckTurn(player, TurnPhase.Play);
            if (check != MoveResult.Ok) return check;
            if (!Racks[player].Contains(tile)) return MoveResult.TileNotInRack;
            if (taken != null) return MoveResult.MustUseTakenTile;

            bool finishing = Racks[player].Count == 1;
            if (Rules.Penalties && !finishing)
            {
                if (Okey.IsWild(tile)) penalties[player] += RuleSet.PenaltyPoints;
                if (FitsAnyMeld(tile)) penalties[player] += RuleSet.PenaltyPoints;
            }

            Racks[player].Remove(tile);
            Discards[player].Add(tile);

            if (finishing)
            {
                Winner = player;
                FinishedWithOkey = Okey.IsWild(tile);
                Phase = TurnPhase.Finished;
            }
            else if (Stock.Count == 0)
            {
                Phase = TurnPhase.Finished;
            }
            else
            {
                Current = (player + 1) % Players;
                Phase = TurnPhase.Draw;
            }
            return MoveResult.Ok;
        }

        /// <summary>Round scores, lowest is best. Only meaningful once the round is finished.</summary>
        public int[] Scores()
        {
            var scores = new int[Players];
            for (int p = 0; p < Players; p++)
            {
                if (p == Winner)
                {
                    scores[p] = RuleSet.WinnerPoints;
                }
                else if (opened[p] == OpenKind.None)
                {
                    scores[p] = RuleSet.UnopenedPoints;
                }
                else
                {
                    foreach (var tile in Racks[p]) scores[p] += Okey.HandValue(tile);
                    if (opened[p] == OpenKind.Pairs) scores[p] *= 2;
                }

                if (FinishedWithOkey) scores[p] *= 2;
                scores[p] += penalties[p];
            }
            return scores;
        }

        MoveResult CheckTurn(int player, TurnPhase phase)
        {
            if (Phase == TurnPhase.Finished) return MoveResult.WrongPhase;
            if (player != Current) return MoveResult.NotYourTurn;
            return Phase == phase ? MoveResult.Ok : MoveResult.WrongPhase;
        }

        static int Previous(int player) => (player + Players - 1) % Players;

        bool HasAll(int player, IReadOnlyList<Tile> tiles)
        {
            var seen = new HashSet<Tile>();
            foreach (var tile in tiles)
            {
                if (!seen.Add(tile) || !Racks[player].Contains(tile)) return false;
            }
            return true;
        }

        void PutOnTable(int player, IReadOnlyList<Tile> meld, MeldKind kind)
        {
            foreach (var tile in meld)
            {
                Racks[player].Remove(tile);
                if (taken == tile) taken = null;
            }
            table.Add(new TableMeld(player, kind, new List<Tile>(meld)));
        }

        bool Fits(TableMeld meld, Tile tile, bool atFront, out MeldKind kind)
        {
            kind = meld.Kind;
            if (meld.Kind == MeldKind.Pair) return false;

            var extended = new List<Tile>(meld.Tiles);
            extended.Insert(atFront ? 0 : extended.Count, tile);
            if (!MeldRules.TryEvaluate(extended, Okey, Rules, out var info)) return false;

            kind = info.Kind;
            return true;
        }

        /// <summary>True when the tile could be added to a run or set on the table.</summary>
        public bool FitsAnyMeld(Tile tile)
        {
            foreach (var meld in table)
            {
                if (Fits(meld, tile, true, out _) || Fits(meld, tile, false, out _)) return true;
            }
            return false;
        }
    }
}
