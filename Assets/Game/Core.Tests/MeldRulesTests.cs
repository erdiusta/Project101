using NUnit.Framework;

namespace Project101.Core.Tests
{
    public class MeldRulesTests
    {
        // Indicator Red 4 makes Red 5 the okey.
        static readonly Okey Okey = Okey.FromIndicator(Tile.Of(TileColor.Red, 4));
        static readonly Tile Wild = Tile.Of(TileColor.Red, 5);

        static bool Evaluate(out MeldInfo info, RuleSet rules, params Tile[] tiles) =>
            MeldRules.TryEvaluate(tiles, Okey, rules, out info);

        static bool Evaluate(out MeldInfo info, params Tile[] tiles) => Evaluate(out info, new RuleSet(), tiles);

        [Test]
        public void TileIdsRoundTrip()
        {
            for (int copy = 0; copy < 2; copy++)
            for (int color = 0; color < 4; color++)
            for (int number = 1; number <= 13; number++)
            {
                var tile = Tile.Of((TileColor)color, number, copy);
                Assert.AreEqual((TileColor)color, tile.Color);
                Assert.AreEqual(number, tile.Number);
                Assert.IsFalse(tile.IsFalseJoker);
            }
            Assert.IsTrue(Tile.FalseJoker(1).IsFalseJoker);
        }

        [Test]
        public void OkeyWrapsFromThirteenToOne()
        {
            var okey = Okey.FromIndicator(Tile.Of(TileColor.Blue, 13));
            Assert.IsTrue(okey.IsWild(Tile.Of(TileColor.Blue, 1, 1)));
            Assert.IsFalse(okey.IsWild(Tile.Of(TileColor.Black, 1)));
        }

        [Test]
        public void RunScoresTheSumOfItsNumbers()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Blue, 7), Tile.Of(TileColor.Blue, 8), Tile.Of(TileColor.Blue, 9)));
            Assert.AreEqual(MeldKind.Run, info.Kind);
            Assert.AreEqual(24, info.Points);
        }

        [Test]
        public void RunRejectsMixedColorsGapsAndShortGroups()
        {
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 7), Tile.Of(TileColor.Black, 8), Tile.Of(TileColor.Blue, 9)));
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 7), Tile.Of(TileColor.Blue, 9), Tile.Of(TileColor.Blue, 10)));
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 7), Tile.Of(TileColor.Blue, 9)));
        }

        [Test]
        public void WildTakesTheValueOfItsPositionInARun()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Black, 11), Wild, Tile.Of(TileColor.Black, 13)));
            Assert.AreEqual(MeldKind.Run, info.Kind);
            Assert.AreEqual(36, info.Points);

            Assert.IsTrue(Evaluate(out info, Wild, Tile.Of(TileColor.Black, 2), Tile.Of(TileColor.Black, 3)));
            Assert.AreEqual(6, info.Points);

            // Nothing sits below 1, so a wild cannot lead a run that starts there.
            Assert.IsFalse(Evaluate(out _, Wild, Tile.Of(TileColor.Black, 1), Tile.Of(TileColor.Black, 2)));
        }

        [Test]
        public void OneAfterThirteenNeedsTheWrapRule()
        {
            var tiles = new[] { Tile.Of(TileColor.Yellow, 12), Tile.Of(TileColor.Yellow, 13), Tile.Of(TileColor.Yellow, 1) };
            Assert.IsFalse(Evaluate(out _, tiles));

            var wrap = new RuleSet { WrapRuns = true };
            Assert.IsTrue(Evaluate(out var info, wrap, tiles));
            Assert.AreEqual(26, info.Points);
            Assert.IsTrue(Evaluate(out _, wrap, Wild, Tile.Of(TileColor.Yellow, 13), Tile.Of(TileColor.Yellow, 1)));
            Assert.IsFalse(Evaluate(out _, wrap, Tile.Of(TileColor.Yellow, 13), Tile.Of(TileColor.Yellow, 1), Tile.Of(TileColor.Yellow, 2)));
        }

        [Test]
        public void SetNeedsDistinctColors()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Blue, 9), Tile.Of(TileColor.Black, 9), Tile.Of(TileColor.Yellow, 9)));
            Assert.AreEqual(MeldKind.Set, info.Kind);
            Assert.AreEqual(27, info.Points);

            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 9), Tile.Of(TileColor.Blue, 9, 1), Tile.Of(TileColor.Yellow, 9)));
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 9), Tile.Of(TileColor.Black, 9), Tile.Of(TileColor.Yellow, 9),
                Tile.Of(TileColor.Red, 9), Wild));
        }

        [Test]
        public void AmbiguousGroupCountsAsTheHigherMeld()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Blue, 13), Wild, Wild));
            Assert.AreEqual(MeldKind.Set, info.Kind);
            Assert.AreEqual(39, info.Points);

            Assert.IsTrue(Evaluate(out info, Tile.Of(TileColor.Blue, 5), Wild, Wild));
            Assert.AreEqual(MeldKind.Run, info.Kind);
            Assert.AreEqual(18, info.Points);
        }

        [Test]
        public void FalseJokerStandsForTheOkeyFace()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Red, 4), Tile.FalseJoker(), Tile.Of(TileColor.Red, 6)));
            Assert.AreEqual(15, info.Points);
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 4), Tile.FalseJoker(), Tile.Of(TileColor.Blue, 6)));
        }

        [Test]
        public void PairIsTwoIdenticalTiles()
        {
            Assert.IsTrue(Evaluate(out var info, Tile.Of(TileColor.Blue, 3), Tile.Of(TileColor.Blue, 3, 1)));
            Assert.AreEqual(MeldKind.Pair, info.Kind);
            Assert.IsTrue(Evaluate(out _, Tile.Of(TileColor.Blue, 3), Wild));
            Assert.IsFalse(Evaluate(out _, Tile.Of(TileColor.Blue, 3), Tile.Of(TileColor.Black, 3)));
        }
    }
}
