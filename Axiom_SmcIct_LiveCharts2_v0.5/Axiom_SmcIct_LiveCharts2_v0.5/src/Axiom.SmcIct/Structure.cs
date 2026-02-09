using Axiom.Core;

namespace Axiom.SmcIct;

internal readonly record struct SwingPoint(int Index, DateTime Time, double Price, bool IsHigh);

internal static class Structure
{
    /// <summary>
    /// Pivot swings using a symmetric window (len left and len right).
    /// Uses strict comparisons to avoid duplicates.
    /// </summary>
    public static List<SwingPoint> PivotSwings(IReadOnlyList<Candle> bars, int len)
    {
        var swings = new List<SwingPoint>();
        if (bars.Count < 2 * len + 1) return swings;

        for (int i = len; i < bars.Count - len; i++)
        {
            double hi = bars[i].High;
            double lo = bars[i].Low;

            bool isPH = true;
            bool isPL = true;

            for (int j = i - len; j <= i + len; j++)
            {
                if (j == i) continue;
                if (bars[j].High >= hi) isPH = false;
                if (bars[j].Low <= lo) isPL = false;
                if (!isPH && !isPL) break;
            }

            if (isPH) swings.Add(new SwingPoint(i, bars[i].Time, hi, true));
            if (isPL) swings.Add(new SwingPoint(i, bars[i].Time, lo, false));
        }

        swings.Sort((a, b) => a.Index.CompareTo(b.Index));
        return swings;
    }

    public static double EstimateTick(IReadOnlyList<Candle> bars)
    {
        // Estimate tick from observed price precision.
        // Fallback to 1e-5.
        double tick = double.PositiveInfinity;
        for (int i = 1; i < bars.Count; i++)
        {
            double d = Math.Abs(bars[i].Close - bars[i - 1].Close);
            if (d > 0 && d < tick) tick = d;
        }

        if (double.IsInfinity(tick) || tick <= 0) return 1e-5;

        // snap to a power-of-10-ish
        double pow = Math.Pow(10, Math.Floor(Math.Log10(tick)));
        return Math.Round(tick / pow) * pow;
    }
}
