using Axiom.Core;

namespace Axiom.SmcIct;

internal readonly record struct SwingPoint(
    int PivotIndex,
    int ConfirmIndex,
    DateTime PivotTime,
    DateTime ConfirmTime,
    double Price,
    bool IsHigh);

internal static class Structure
{
    /// <summary>
    /// Deterministic swings:
    /// - A pivot at i is only available at i+len (confirm index), so no-lookahead consumers must
    ///   only use points with ConfirmIndex <= current bar index.
    /// </summary>
    public static List<SwingPoint> ConfirmedPivotSwings(IReadOnlyList<Candle> bars, int len)
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

            int confirm = i + len;
            if (isPH) swings.Add(new SwingPoint(i, confirm, bars[i].Time, bars[confirm].Time, hi, true));
            if (isPL) swings.Add(new SwingPoint(i, confirm, bars[i].Time, bars[confirm].Time, lo, false));
        }

        swings.Sort((a, b) => a.ConfirmIndex.CompareTo(b.ConfirmIndex));
        return swings;
    }

    /// <summary>
    /// Non-deterministic legacy (lookahead) pivots; retained for comparison.
    /// </summary>
    public static List<SwingPoint> LegacyPivotSwings(IReadOnlyList<Candle> bars, int len)
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

            if (isPH) swings.Add(new SwingPoint(i, i, bars[i].Time, bars[i].Time, hi, true));
            if (isPL) swings.Add(new SwingPoint(i, i, bars[i].Time, bars[i].Time, lo, false));
        }

        swings.Sort((a, b) => a.ConfirmIndex.CompareTo(b.ConfirmIndex));
        return swings;
    }

    public static List<SwingPoint> PivotSwings(IReadOnlyList<Candle> bars, int len, bool deterministic)
        => deterministic ? ConfirmedPivotSwings(bars, len) : LegacyPivotSwings(bars, len);

    public static double EstimateTick(IReadOnlyList<Candle> bars)
    {
        double tick = double.PositiveInfinity;
        for (int i = 1; i < bars.Count; i++)
        {
            double d = Math.Abs(bars[i].Close - bars[i - 1].Close);
            if (d > 0 && d < tick) tick = d;
        }

        if (double.IsInfinity(tick) || tick <= 0) return 1e-5;

        double pow = Math.Pow(10, Math.Floor(Math.Log10(tick)));
        return Math.Round(tick / pow) * pow;
    }
}
