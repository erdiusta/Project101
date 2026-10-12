namespace Project101.Core
{
    /// <summary>Table rules chosen by the host before a game starts.</summary>
    public sealed class RuleSet
    {
        /// <summary>Points needed to open with runs and sets.</summary>
        public int OpeningPoints = 101;

        /// <summary>Pairs needed to open with pairs.</summary>
        public int OpeningPairs = 5;

        /// <summary>Katlamali: each opening must beat the previous one by a point (or a pair).</summary>
        public bool Progressive;

        /// <summary>Rounds in a match.</summary>
        public int Rounds = 7;

        /// <summary>Charge 101 for discarding an okey or a tile that fits a meld on the table.</summary>
        public bool Penalties = true;

        /// <summary>Allow 12-13-1 as a run.</summary>
        public bool WrapRuns;

        public const int PenaltyPoints = 101;
        public const int WinnerPoints = -101;
        public const int UnopenedPoints = 202;
    }
}
