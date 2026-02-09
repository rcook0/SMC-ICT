using Axiom.Core;

namespace Axiom.SmcIct;

/// <summary>
/// Stub strategy engine.
/// Replace the internals module-by-module, keeping the contract stable.
/// </summary>
public sealed class SmcIctEngine
{
    private readonly SmcIctParameters _p;

    public SmcIctEngine(SmcIctParameters? parameters = null)
    {
        _p = parameters ?? new SmcIctParameters();
    }

    public AnalysisResult Analyze(IReadOnlyList<Candle> candles, string symbol = "SYMBOL", string tf = Timeframes.M5)
    {
        if (candles.Count < 50)
            return new AnalysisResult(Array.Empty<Zone>(), Array.Empty<SignalEvent>());

        var zones = new List<Zone>();
        var sigs  = new List<SignalEvent>();

        // --- STUB PIPELINE IDEA ---
        // 1) Build a fake dealing range using last N candles.
        // 2) Emit a synthetic "zone" every N bars.
        // 3) Emit a signal when close crosses a simple moving average.
        // This gives UI/backtest something deterministic to render.

        int n = Math.Min(200, candles.Count);
        double hi = candles.Skip(candles.Count - n).Max(c => c.High);
        double lo = candles.Skip(candles.Count - n).Min(c => c.Low);

        zones.Add(new Zone(
            Id: Guid.NewGuid(),
            Type: ZoneType.DealingRange,
            Tf: "HTF",
            Created: candles[^n].Time,
            Expires: null,
            Low: lo,
            High: hi,
            IsBullish: candles[^1].Close >= candles[^n].Close,
            Meta: $"stub dealing range over last {n} bars"
        ));

        // Simple SMA for stub signals
        int smaLen = 20;
        var sma = new double[candles.Count];
        double sum = 0;

        for (int i = 0; i < candles.Count; i++)
        {
            sum += candles[i].Close;
            if (i >= smaLen) sum -= candles[i - smaLen].Close;
            sma[i] = i >= smaLen - 1 ? sum / smaLen : double.NaN;
        }

        // Emit zones periodically + signals on SMA cross
        int zoneEvery = 80;
        for (int i = smaLen; i < candles.Count; i++)
        {
            var c = candles[i];
            var prev = candles[i - 1];

            if (i % zoneEvery == 0)
            {
                double zLow  = Math.Min(c.Low, prev.Low);
                double zHigh = Math.Max(c.High, prev.High);
                bool bullish = c.Close >= c.Open;

                zones.Add(new Zone(
                    Id: Guid.NewGuid(),
                    Type: bullish ? ZoneType.OrderBlock : ZoneType.FairValueGap,
                    Tf: Timeframes.M15,
                    Created: c.Time,
                    Expires: null,
                    Low: zLow,
                    High: zHigh,
                    IsBullish: bullish,
                    Meta: "stub zone (replace with OB/FVG/Breaker lifecycle)"
                ));
            }

            // SMA cross signals
            if (double.IsNaN(sma[i - 1]) || double.IsNaN(sma[i])) continue;

            bool crossUp = prev.Close <= sma[i - 1] && c.Close > sma[i];
            bool crossDn = prev.Close >= sma[i - 1] && c.Close < sma[i];

            if (crossUp)
            {
                sigs.Add(new SignalEvent(
                    Time: c.Time,
                    Side: Side.Buy,
                    ZoneId: zones.LastOrDefault().Id,
                    EntryHint: c.Close,
                    SlHint: c.Low,
                    Tp1Hint: c.Close + (c.Close - c.Low),
                    Tp2Hint: c.Close + 2 * (c.Close - c.Low),
                    Reasons: new[] { "stub:SMA_CROSS_UP", "stub:ZONE_PRESENT" }
                ));
            }

            if (crossDn)
            {
                sigs.Add(new SignalEvent(
                    Time: c.Time,
                    Side: Side.Sell,
                    ZoneId: zones.LastOrDefault().Id,
                    EntryHint: c.Close,
                    SlHint: c.High,
                    Tp1Hint: c.Close - (c.High - c.Close),
                    Tp2Hint: c.Close - 2 * (c.High - c.Close),
                    Reasons: new[] { "stub:SMA_CROSS_DN", "stub:ZONE_PRESENT" }
                ));
            }
        }

        return new AnalysisResult(zones, sigs);
    }
}
