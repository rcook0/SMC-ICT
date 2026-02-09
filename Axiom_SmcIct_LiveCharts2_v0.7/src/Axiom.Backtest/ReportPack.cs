using Axiom.Core;

namespace Axiom.Backtest;

public sealed record EquityPoint(DateTime Time, double EquityR, double DrawdownR);

public static class ReportPack
{
    public static List<EquityPoint> EquityCurveR(IReadOnlyList<TradeResult> trades)
    {
        var pts = new List<EquityPoint>();
        double eq = 0;
        double peak = 0;

        foreach (var t in trades.OrderBy(x => x.ExitTime))
        {
            eq += t.RMultiple;
            peak = Math.Max(peak, eq);
            double dd = peak - eq;
            pts.Add(new EquityPoint(t.ExitTime, eq, dd));
        }

        return pts;
    }

    public static void SaveEquityCsv(string path, IReadOnlyList<EquityPoint> pts)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("time,equity_r,drawdown_r");
        foreach (var p in pts)
            w.WriteLine($"{p.Time:O},{p.EquityR.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.DrawdownR.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }
}
