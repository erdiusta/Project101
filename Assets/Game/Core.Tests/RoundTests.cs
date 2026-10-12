using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Project101.Core.Tests
{
    public class RoundTests
    {
        static Tile T(TileColor color, int number, int copy = 0) => Tile.Of(color, number, copy);

        static Tile[] Run(TileColor color, int from, int to, int copy = 0) =>
            Enumerable.Range(from, to - from + 1).Select(n => T(color, n, copy)).ToArray();

        // A round with the okey fixed at Red 5 and seat 0 holding exactly the given tiles, ready to play.
        static Round RoundWithRack(RuleSet rules, params Tile[] rack)
        {
            var round = new Round(rules, 1);
            round.SetIndicator(T(TileColor.Red, 4));
            SetRack(round, 0, rack);
            return round;
        }

        static void SetRack(Round round, int player, params Tile[] rack)
        {
            round.Racks[player].Clear();
            round.Racks[player].AddRange(rack);
        }

        // Blue 10-13 (46) + Black 10-13 (46) + Yellow 3-5 (12) = 104 points.
        static readonly Tile[][] Opening104 =
        {
            Run(TileColor.Blue, 10, 13), Run(TileColor.Black, 10, 13), Run(TileColor.Yellow, 3, 5)
        };

        static Tile[] Flatten(IEnumerable<Tile[]> melds, params Tile[] extra) =>
            melds.SelectMany(m => m).Concat(extra).ToArray();

        [Test]
        public void DealGivesTheStarterTheExtraTile()
        {
            var round = new Round(new RuleSet(), 42, starter: 2);

            for (int p = 0; p < Round.Players; p++) Assert.AreEqual(p == 2 ? 22 : 21, round.Rack(p).Count);
            Assert.AreEqual(20, round.StockCount);
            Assert.AreEqual(2, round.Current);
            Assert.AreEqual(TurnPhase.Play, round.Phase);
            Assert.IsFalse(round.Indicator.IsFalseJoker);

            var all = Enumerable.Range(0, Round.Players).SelectMany(p => round.Rack(p))
                .Concat(round.Stock).Append(round.Indicator).Select(t => t.Id).ToList();
            Assert.AreEqual(Tile.Count, all.Distinct().Count());
        }

        [Test]
        public void SameSeedDealsTheSameRound()
        {
            var a = new Round(new RuleSet(), 7);
            var b = new Round(new RuleSet(), 7);
            CollectionAssert.AreEqual(a.Rack(0), b.Rack(0));
            Assert.AreEqual(a.Indicator, b.Indicator);
        }

        [Test]
        public void MovesOutOfTurnOrPhaseAreRejected()
        {
            var round = new Round(new RuleSet(), 42);
            Assert.AreEqual(MoveResult.NotYourTurn, round.DrawFromStock(1));
            Assert.AreEqual(MoveResult.WrongPhase, round.DrawFromStock(0));
            Assert.AreEqual(MoveResult.TileNotInRack, round.Discard(0, round.Rack(1)[0]));

            Assert.AreEqual(MoveResult.Ok, round.Discard(0, round.Rack(0)[0]));
            Assert.AreEqual(1, round.Current);
            Assert.AreEqual(MoveResult.WrongPhase, round.Discard(1, round.Rack(1)[0]));
        }

        [Test]
        public void OpeningNeedsTheRequiredPoints()
        {
            var spare = T(TileColor.Red, 9);
            var round = RoundWithRack(new RuleSet(), Flatten(Opening104, spare));

            Assert.AreEqual(MoveResult.OpeningTooLow, round.Open(0, Opening104.Take(2).ToArray()));
            Assert.AreEqual(OpenKind.None, round.Opened(0));

            Assert.AreEqual(MoveResult.Ok, round.Open(0, Opening104));
            Assert.AreEqual(OpenKind.Melds, round.Opened(0));
            Assert.AreEqual(3, round.Table.Count);
            CollectionAssert.AreEqual(new[] { spare }, round.Rack(0));
            Assert.AreEqual(MoveResult.AlreadyOpened, round.Open(0, Opening104));
        }

        [Test]
        public void OpeningRejectsTilesThePlayerDoesNotHold()
        {
            var round = RoundWithRack(new RuleSet(), Flatten(Opening104.Take(2)));
            Assert.AreEqual(MoveResult.TileNotInRack, round.Open(0, Opening104));

            // The same physical tile cannot be used in two melds.
            var twice = new[] { Opening104[0], Opening104[0], Opening104[1] };
            Assert.AreEqual(MoveResult.TileNotInRack, round.Open(0, twice));
        }

        [Test]
        public void FivePairsOpenAndMixedOpeningsDoNot()
        {
            var pairs = Enumerable.Range(1, 5).Select(n => new[] { T(TileColor.Blue, n), T(TileColor.Blue, n, 1) }).ToArray();
            var round = RoundWithRack(new RuleSet(), Flatten(pairs.Append(Run(TileColor.Black, 10, 13)), T(TileColor.Red, 9)));

            Assert.AreEqual(MoveResult.OpeningTooLow, round.Open(0, pairs.Take(4).ToArray()));
            Assert.AreEqual(MoveResult.MixedOpening, round.Open(0, pairs.Append(Run(TileColor.Black, 10, 13)).ToArray()));
            Assert.AreEqual(MoveResult.Ok, round.Open(0, pairs));
            Assert.AreEqual(OpenKind.Pairs, round.Opened(0));

            // A pair opener keeps to pairs.
            Assert.AreEqual(MoveResult.WrongMeldKind, round.Lay(0, Run(TileColor.Black, 10, 13)));
        }

        [Test]
        public void ProgressiveTableRaisesTheNextOpening()
        {
            var round = RoundWithRack(new RuleSet { Progressive = true }, Flatten(Opening104, T(TileColor.Red, 9)));
            Assert.AreEqual(101, round.RequiredPoints);
            round.Open(0, Opening104);
            Assert.AreEqual(105, round.RequiredPoints);

            var plain = RoundWithRack(new RuleSet(), Flatten(Opening104, T(TileColor.Red, 9)));
            plain.Open(0, Opening104);
            Assert.AreEqual(101, plain.RequiredPoints);
        }

        [Test]
        public void TilesCanBeAddedToTableMeldsOnlyAfterOpening()
        {
            var blue9 = T(TileColor.Blue, 9);
            var yellow6 = T(TileColor.Yellow, 6);
            var red9 = T(TileColor.Red, 9);
            var round = RoundWithRack(new RuleSet(), Flatten(Opening104, blue9, yellow6, red9, T(TileColor.Red, 1)));

            Assert.AreEqual(MoveResult.NotOpened, round.Lay(0, Run(TileColor.Blue, 10, 12)));
            round.Open(0, Opening104);

            Assert.AreEqual(MoveResult.InvalidMeld, round.AddToMeld(0, 0, blue9, atFront: false));
            Assert.AreEqual(MoveResult.Ok, round.AddToMeld(0, 0, blue9, atFront: true));
            Assert.AreEqual(MoveResult.Ok, round.AddToMeld(0, 2, yellow6, atFront: false));
            Assert.AreEqual(MoveResult.InvalidMeld, round.AddToMeld(0, 1, red9, atFront: true));
            Assert.AreEqual(MoveResult.BadMeldIndex, round.AddToMeld(0, 3, red9, atFront: true));
            CollectionAssert.AreEqual(Run(TileColor.Blue, 9, 13), round.Table[0].Tiles);
        }

        [Test]
        public void TakenDiscardMustBeMeldedOrReturned()
        {
            var round = new Round(new RuleSet(), 42);
            round.SetIndicator(T(TileColor.Red, 4));
            var blue9 = T(TileColor.Blue, 9);
            SetRack(round, 0, blue9, T(TileColor.Red, 1));
            round.Discard(0, blue9);

            var spare = T(TileColor.Red, 9);
            SetRack(round, 1, Flatten(Opening104, spare));
            Assert.AreEqual(MoveResult.Ok, round.TakeDiscard(1));
            Assert.AreEqual(MoveResult.MustUseTakenTile, round.Discard(1, spare));

            Assert.AreEqual(MoveResult.Ok, round.ReturnTakenDiscard(1));
            CollectionAssert.AreEqual(new[] { blue9 }, round.DiscardPile(0));
            Assert.AreEqual(MoveResult.MustDrawFromStock, round.TakeDiscard(1));
            Assert.AreEqual(MoveResult.Ok, round.DrawFromStock(1));
            Assert.AreEqual(MoveResult.NothingToReturn, round.ReturnTakenDiscard(1));
        }

        [Test]
        public void TakenDiscardCanBeUsedInTheOpening()
        {
            var round = new Round(new RuleSet(), 42);
            round.SetIndicator(T(TileColor.Red, 4));
            var blue13 = T(TileColor.Blue, 13);
            SetRack(round, 0, blue13, T(TileColor.Red, 1));
            round.Discard(0, blue13);

            var spare = T(TileColor.Red, 9);
            SetRack(round, 1, Flatten(Opening104, spare).Where(t => t != blue13).ToArray());
            round.TakeDiscard(1);
            Assert.AreEqual(MoveResult.Ok, round.Open(1, Opening104));
            Assert.AreEqual(MoveResult.Ok, round.Discard(1, spare));
            Assert.AreEqual(1, round.Winner);
        }

        [Test]
        public void DiscardingAnOkeyOrAFittingTileIsPenalised()
        {
            var wild = T(TileColor.Red, 5);
            var blue9 = T(TileColor.Blue, 9);
            var round = RoundWithRack(new RuleSet(), Flatten(Opening104, wild, blue9, T(TileColor.Red, 1)));
            round.Open(0, Opening104);
            round.Discard(0, blue9);
            Assert.AreEqual(101, round.Penalty(0));

            var noPenalties = RoundWithRack(new RuleSet { Penalties = false }, Flatten(Opening104, wild, blue9));
            noPenalties.Open(0, Opening104);
            noPenalties.Discard(0, wild);
            Assert.AreEqual(0, noPenalties.Penalty(0));

            // A wild fits every meld, so throwing it away costs both penalties.
            var both = RoundWithRack(new RuleSet(), Flatten(Opening104, wild, blue9));
            both.Open(0, Opening104);
            both.Discard(0, wild);
            Assert.AreEqual(202, both.Penalty(0));
        }

        [Test]
        public void FinishingScoresTheRound()
        {
            var last = T(TileColor.Red, 9);
            var round = RoundWithRack(new RuleSet(), Flatten(Opening104, last));
            SetRack(round, 1, T(TileColor.Red, 13), T(TileColor.Red, 2), Tile.FalseJoker());
            round.Open(0, Opening104);
            Assert.AreEqual(MoveResult.Ok, round.Discard(0, last));

            Assert.AreEqual(TurnPhase.Finished, round.Phase);
            Assert.AreEqual(0, round.Winner);
            Assert.AreEqual(0, round.Penalty(0));
            CollectionAssert.AreEqual(new[] { -101, 202, 202, 202 }, round.Scores());
            Assert.AreEqual(MoveResult.WrongPhase, round.DrawFromStock(1));
        }

        [Test]
        public void OpenedLosersPayTheirRackAndAnOkeyFinishDoublesEverything()
        {
            var wild = T(TileColor.Red, 5);
            var pairs = Enumerable.Range(1, 5).Select(n => new[] { T(TileColor.Yellow, n), T(TileColor.Yellow, n, 1) }).ToArray();

            var round = new Round(new RuleSet(), 42);
            round.SetIndicator(T(TileColor.Red, 4));

            // Seat 0 opens with melds and is left with Red 13 + false joker (5) = 18.
            SetRack(round, 0, Flatten(Opening104, T(TileColor.Red, 13), Tile.FalseJoker(), T(TileColor.Red, 1)));
            round.Open(0, Opening104);
            round.Discard(0, T(TileColor.Red, 1));

            // Seat 1 opens with pairs and is left with Black 7, doubled to 14.
            round.DrawFromStock(1);
            var drawn = round.Rack(1)[round.Rack(1).Count - 1];
            SetRack(round, 1, Flatten(pairs, T(TileColor.Black, 7), drawn));
            round.Open(1, pairs);
            round.Discard(1, drawn);
            int seat1Penalty = round.Penalty(1);

            // Seat 2 finishes by discarding the okey.
            round.DrawFromStock(2);
            SetRack(round, 2, Flatten(Opening104.Select(m => m.Select(t => T(t.Color, t.Number, 1)).ToArray()), wild));
            round.Open(2, Opening104.Select(m => m.Select(t => T(t.Color, t.Number, 1)).ToArray()).ToArray());
            round.Discard(2, wild);

            Assert.IsTrue(round.FinishedWithOkey);
            CollectionAssert.AreEqual(new[] { 36, 28 + seat1Penalty, -202, 404 }, round.Scores());
        }

        [Test]
        public void RoundEndsWithoutAWinnerWhenTheStockRunsOut()
        {
            var round = new Round(new RuleSet { Penalties = false }, 42);
            round.Discard(0, round.Rack(0)[0]);
            while (round.Phase != TurnPhase.Finished)
            {
                int player = round.Current;
                Assert.AreEqual(MoveResult.Ok, round.DrawFromStock(player));
                Assert.AreEqual(MoveResult.Ok, round.Discard(player, round.Rack(player)[0]));
            }

            Assert.AreEqual(-1, round.Winner);
            Assert.AreEqual(0, round.StockCount);
            CollectionAssert.AreEqual(new[] { 202, 202, 202, 202 }, round.Scores());
        }
    }
}
