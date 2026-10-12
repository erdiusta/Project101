using System;

namespace Project101.Core
{
    public enum TileColor : byte
    {
        Yellow,
        Blue,
        Black,
        Red
    }

    /// <summary>
    /// One of the 106 physical tiles. Ids 0-103 are two copies of 4 colors x 13 numbers,
    /// ids 104 and 105 are the false jokers.
    /// </summary>
    public readonly struct Tile : IEquatable<Tile>
    {
        public const int Count = 106;
        const int FirstFalseJoker = 104;

        public readonly byte Id;

        public Tile(int id)
        {
            if ((uint)id >= Count) throw new ArgumentOutOfRangeException(nameof(id));
            Id = (byte)id;
        }

        public static Tile Of(TileColor color, int number, int copy = 0)
        {
            if (number < 1 || number > 13) throw new ArgumentOutOfRangeException(nameof(number));
            if (copy < 0 || copy > 1) throw new ArgumentOutOfRangeException(nameof(copy));
            return new Tile(copy * 52 + (int)color * 13 + number - 1);
        }

        public static Tile FalseJoker(int copy = 0) => new Tile(FirstFalseJoker + copy);

        public bool IsFalseJoker => Id >= FirstFalseJoker;
        public TileColor Color => (TileColor)(Id % 52 / 13);
        public int Number => Id % 13 + 1;

        public bool Equals(Tile other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Tile other && Equals(other);
        public override int GetHashCode() => Id;
        public static bool operator ==(Tile a, Tile b) => a.Id == b.Id;
        public static bool operator !=(Tile a, Tile b) => a.Id != b.Id;
        public override string ToString() => IsFalseJoker ? "FalseJoker" : $"{Color}{Number}";
    }

    /// <summary>What a tile stands for once the round's okey is known.</summary>
    public readonly struct Face
    {
        public readonly bool IsWild;
        public readonly TileColor Color;
        public readonly int Number;

        public Face(bool isWild, TileColor color, int number)
        {
            IsWild = isWild;
            Color = color;
            Number = number;
        }
    }

    /// <summary>The wild tile of a round: same color as the indicator, one number higher (13 wraps to 1).</summary>
    public readonly struct Okey
    {
        public readonly TileColor Color;
        public readonly int Number;

        public Okey(TileColor color, int number)
        {
            Color = color;
            Number = number;
        }

        public static Okey FromIndicator(Tile indicator)
        {
            if (indicator.IsFalseJoker) throw new ArgumentException("A false joker cannot be the indicator.", nameof(indicator));
            return new Okey(indicator.Color, indicator.Number % 13 + 1);
        }

        public bool IsWild(Tile tile) => !tile.IsFalseJoker && tile.Color == Color && tile.Number == Number;

        /// <summary>False jokers stand for the okey's own face; the real okey tiles are wild.</summary>
        public Face Resolve(Tile tile) =>
            tile.IsFalseJoker ? new Face(false, Color, Number) : new Face(IsWild(tile), tile.Color, tile.Number);

        /// <summary>Points a tile costs when left in the rack at the end of a round.</summary>
        public int HandValue(Tile tile) => tile.IsFalseJoker ? Number : tile.Number;
    }
}
