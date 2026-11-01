using System;
using System.Globalization;

namespace GodGame.Sim
{
    /// <summary>
    /// A handle to a unit or a regiment: a slot index plus the slot's generation when the handle was made.
    /// A freed slot's generation moves on, so old handles stop matching and can never reach the slot's new owner.
    /// Generation 0 is never issued, so the default value is <see cref="None"/>.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId None = default;

        public readonly int Index;
        public readonly int Generation;

        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool IsNone => Generation == 0;

        public static bool operator ==(EntityId a, EntityId b) =>
            a.Index == b.Index && a.Generation == b.Generation;

        public static bool operator !=(EntityId a, EntityId b) => !(a == b);

        public bool Equals(EntityId other) => this == other;

        public override bool Equals(object? obj) => obj is EntityId other && this == other;

        public override int GetHashCode() => unchecked(Index * 397 ^ Generation);

        public override string ToString() =>
            IsNone
                ? "none"
                : Index.ToString(CultureInfo.InvariantCulture)
                    + "v"
                    + Generation.ToString(CultureInfo.InvariantCulture);
    }
}
