using Axiom.Core;

namespace Axiom.SmcIct;

internal static class Liquidity
{
    public static bool IsSweepUp(Candle c, double level, double tol)
        => c.High > level + tol && c.Close < level;

    public static bool IsSweepDown(Candle c, double level, double tol)
        => c.Low < level - tol && c.Close > level;

    public static double DefaultSweepTol(double tick, double ticks) => Math.Max(tick, tick * ticks);
}
