using System;

namespace RoyaltyBoat.MapGeneration
{
    [Flags]
    public enum LaneMask
    {
        None = 0,
        Left = 1 << 0,
        Center = 1 << 1,
        Right = 1 << 2,
        All = Left | Center | Right
    }
}
