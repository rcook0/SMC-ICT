using Axiom.Core;

namespace Axiom.Backtest;

public sealed record EconomicsConfig(
    // Spread
    double SpreadTicks = 0.0,
    double SpreadBps = 0.0,

    // Slippage (always adverse)
    double SlippageTicks = 0.0,
    double SlippageBps = 0.0,

    // Commission per side
    double CommissionBps = 0.0,
    double CommissionFixed = 0.0,

    // Position size (unit notional)
    double Quantity = 1.0,

    // v0.7: ATR-aware execution
    string Mode = "Fixed",            // Fixed | Atr
    int AtrLen = 14,
    double AtrSpreadFrac = 0.0,         // spread += AtrSpreadFrac * ATR
    double AtrSlipFrac = 0.0            // slippage += AtrSlipFrac * ATR
);

internal static class Economics
{
    public static double TickEstimate(IReadOnlyList<Candle> bars)
    {
        double tick = double.PositiveInfinity;
        for (int i = 1; i < bars.Count; i++)
        {
            var d = Math.Abs(bars[i].Close - bars[i - 1].Close);
            if (d > 0 && d < tick) tick = d;
        }
        if (double.IsInfinity(tick) || tick <= 0) return 1e-5;
        double pow = Math.Pow(10, Math.Floor(Math.Log10(tick)));
        return Math.Round(tick / pow) * pow;
    }

    public static double Spread(double price, double tick, EconomicsConfig e, double atr = 0.0)
    {
        double baseSpr = 0.0;
        if (e.SpreadTicks > 0) baseSpr = e.SpreadTicks * tick;
        else if (e.SpreadBps > 0) baseSpr = price * (e.SpreadBps / 10000.0);

        if (e.Mode.Equals("Atr", StringComparison.OrdinalIgnoreCase) && e.AtrSpreadFrac > 0 && atr > 0)
            baseSpr += e.AtrSpreadFrac * atr;

        return baseSpr;
    }

    public static double Slippage(double price, double tick, EconomicsConfig e, double atr = 0.0)
    {
        double baseSlip = 0.0;
        if (e.SlippageTicks > 0) baseSlip = e.SlippageTicks * tick;
        else if (e.SlippageBps > 0) baseSlip = price * (e.SlippageBps / 10000.0);

        if (e.Mode.Equals("Atr", StringComparison.OrdinalIgnoreCase) && e.AtrSlipFrac > 0 && atr > 0)
            baseSlip += e.AtrSlipFrac * atr;

        return baseSlip;
    }

    public static double CommissionPerSide(double price, EconomicsConfig e)
    {
        double c = 0;
        if (e.CommissionBps > 0) c += price * (e.CommissionBps / 10000.0) * e.Quantity;
        if (e.CommissionFixed > 0) c += e.CommissionFixed;
        return c;
    }

    public static double FillEntry(double midPrice, Side side, double tick, EconomicsConfig e, double atr = 0.0)
    {
        // Symmetric half-spread, plus adverse slippage
        var spr = Spread(midPrice, tick, e, atr);
        var slip = Slippage(midPrice, tick, e, atr);
        if (side == Side.Buy) return midPrice + spr / 2.0 + slip;
        return midPrice - spr / 2.0 - slip;
    }

    public static double FillExit(double midPrice, Side side, double tick, EconomicsConfig e, double atr = 0.0)
    {
        // Exit is opposite side: sell into bid for longs, buy into ask for shorts, plus adverse slippage
        var spr = Spread(midPrice, tick, e, atr);
        var slip = Slippage(midPrice, tick, e, atr);
        if (side == Side.Buy) return midPrice - spr / 2.0 - slip; // closing long -> sell
        return midPrice + spr / 2.0 + slip; // closing short -> buy
    }
}
