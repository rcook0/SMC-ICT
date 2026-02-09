using Axiom.Core;

namespace Axiom.Backtest;

public sealed record TradeSummary(
    int Trades,
    int Wins,
    int Losses,
    double WinRate,
    double AvgR,
    double SumR,
    double MaxDrawdownR
);

public static class TradeSummary
{
    public static TradeSummary Compute(IReadOnlyList<TradeResult> trades)
    {
        if (trades.Count == 0) return new TradeSummary(0,0,0,0,0,0,0);

        int wins = trades.Count(t => t.RMultiple > 0);
        int losses = trades.Count - wins;
        double sumR = trades.Sum(t => t.RMultiple);
        double avgR = sumR / trades.Count;
        double winRate = (double)wins / trades.Count;

        // Equity curve in R units
        double eq = 0, peak = 0, maxDd = 0;
        foreach (var t in trades)
        {
            eq += t.RMultiple;
            if (eq > peak) peak = eq;
            var dd = peak - eq;
            if (dd > maxDd) maxDd = dd;
        }

        return new TradeSummary(trades.Count, wins, losses, winRate, avgR, sumR, maxDd);
    }
}
