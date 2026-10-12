using System;

namespace Project101.Core
{
    /// <summary>A series of rounds with running totals. The lowest total wins.</summary>
    public sealed class Match
    {
        public readonly RuleSet Rules;
        public readonly int[] Totals = new int[Round.Players];

        public Round Round { get; private set; }

        /// <summary>1 for the first round.</summary>
        public int RoundNumber { get; private set; }

        /// <summary>Scores of the current round once it has been counted, otherwise null.</summary>
        public int[] RoundScores { get; private set; }

        public bool IsOver => RoundScores != null && RoundNumber >= Rules.Rounds;

        readonly Random seeds;

        public Match(RuleSet rules, int seed)
        {
            Rules = rules;
            seeds = new Random(seed);
            NextRound();
        }

        /// <summary>Adds the finished round to the totals. Safe to call more than once.</summary>
        public void ScoreRound()
        {
            if (RoundScores != null || Round.Phase != TurnPhase.Finished) return;
            RoundScores = Round.Scores();
            for (int p = 0; p < Round.Players; p++) Totals[p] += RoundScores[p];
        }

        public void NextRound()
        {
            // The first discard moves one seat on every round.
            Round = new Round(Rules, seeds.Next(), RoundNumber % Round.Players);
            RoundNumber++;
            RoundScores = null;
        }
    }
}
