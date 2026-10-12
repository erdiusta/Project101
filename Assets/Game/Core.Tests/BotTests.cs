using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Project101.Core.Tests
{
    public class BotTests
    {
        static readonly Okey Okey = Okey.FromIndicator(Tile.Of(TileColor.Red, 4));
        static readonly Tile Wild = Tile.Of(TileColor.Red, 5);

        static Tile T(TileColor color, int number, int copy = 0) => Tile.Of(color, number, copy);

        [Test]
        public void ArrangeSortsARunAndPlacesTheWild()
        {
            var loose = new[] { T(TileColor.Blue, 9), Wild, T(TileColor.Blue, 7), T(TileColor.Blue, 10) };
            Assert.IsTrue(MeldRules.TryArrange(loose, Okey, new RuleSet(), out var arranged, out var info));
            CollectionAssert.AreEqual(new[] { T(TileColor.Blue, 7), Wild, T(TileColor.Blue, 9), T(TileColor.Blue, 10) }, arranged);
            Assert.AreEqual(34, info.Points);

            Assert.IsFalse(MeldRules.TryArrange(new[] { T(TileColor.Blue, 9), T(TileColor.Black, 3), T(TileColor.Blue, 7) },
                Okey, new RuleSet(), out _, out _));
        }

        [Test]
        public void ArrangePutsTheOneAfterThirteenOnWrappingTables()
        {
            var loose = new[] { T(TileColor.Blue, 1), T(TileColor.Blue, 13), T(TileColor.Blue, 12) };
            Assert.IsFalse(MeldRules.TryArrange(loose, Okey, new RuleSet(), out _, out _));
            Assert.IsTrue(MeldRules.TryArrange(loose, Okey, new RuleSet { WrapRuns = true }, out var arranged, out _));
            Assert.AreEqual(T(TileColor.Blue, 1), arranged[2]);
        }

        [Test]
        public void FinderPicksRunsSetsAndPairsWithoutReusingTiles()
        {
            var rack = new List<Tile>
            {
                T(TileColor.Blue, 3), T(TileColor.Blue, 4), T(TileColor.Blue, 5), T(TileColor.Blue, 6),
                T(TileColor.Black, 9), T(TileColor.Yellow, 9), T(TileColor.Red, 9),
                T(TileColor.Black, 1), T(TileColor.Black, 1, 1), Wild
            };

            var melds = MeldFinder.FindMelds(rack, Okey);
            Assert.AreEqual(2, melds.Count);
            Assert.AreEqual(45, MeldFinder.Points(melds, Okey));
            Assert.AreEqual(7, melds.SelectMany(m => m).Distinct().Count());
            foreach (var meld in melds) Assert.IsTrue(MeldRules.TryEvaluate(meld, Okey, new RuleSet(), out _));

            var pairs = MeldFinder.FindPairs(rack, Okey);
            Assert.AreEqual(1, pairs.Count);
            CollectionAssert.AreEquivalent(new[] { T(TileColor.Black, 1), T(TileColor.Black, 1, 1) }, pairs[0]);
        }

        [Test]
        public void MeldingMustLeaveATileToDiscard()
        {
            var round = new Round(new RuleSet(), 1);
            round.SetIndicator(T(TileColor.Red, 4));
            var melds = new[]
            {
                Enumerable.Range(10, 4).Select(n => T(TileColor.Blue, n)).ToArray(),
                Enumerable.Range(10, 4).Select(n => T(TileColor.Black, n)).ToArray(),
                Enumerable.Range(3, 3).Select(n => T(TileColor.Yellow, n)).ToArray()
            };
            round.Racks[0].Clear();
            round.Racks[0].AddRange(melds.SelectMany(m => m));

            Assert.AreEqual(MoveResult.MustKeepDiscard, round.Open(0, melds));
        }

        [Test]
        public void BotsPlayWholeMatchesLegally()
        {
            int finished = 0;
            int opened = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var match = new Match(new RuleSet { Rounds = 3 }, seed);
                while (!match.IsOver)
                {
                    if (match.RoundScores != null) match.NextRound();
                    var round = match.Round;
                    for (int turn = 0; round.Phase != TurnPhase.Finished; turn++)
                    {
                        Assert.Less(turn, 200, "round did not end");
                        int player = round.Current;
                        Bot.PlayTurn(round, player);
                        Assert.IsTrue(round.Phase == TurnPhase.Finished || round.Current != player, "bot did not end its turn");
                    }

                    int tiles = Enumerable.Range(0, Round.Players).Sum(p => round.Rack(p).Count + round.DiscardPile(p).Count)
                                + round.Table.Sum(m => m.Tiles.Count) + round.StockCount + 1;
                    Assert.AreEqual(Tile.Count, tiles);
                    foreach (var meld in round.Table) Assert.IsTrue(MeldRules.TryEvaluate(meld.Tiles, round.Okey, match.Rules, out _));

                    if (round.Winner >= 0) finished++;
                    opened += Enumerable.Range(0, Round.Players).Count(p => round.Opened(p) != OpenKind.None);
                    match.ScoreRound();
                }
                Assert.AreEqual(3, match.RoundNumber);
            }

            TestContext.WriteLine($"600 rounds: {finished} finished by a player, {opened} openings");
            Assert.Greater(opened, 0);
        }
    }
}
