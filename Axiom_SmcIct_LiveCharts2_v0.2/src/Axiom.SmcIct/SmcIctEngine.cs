using Axiom.Core;

namespace Axiom.SmcIct;

/// <summary>
/// SMC/ICT engine v0.2: structure primitives.
/// Implemented:
/// - Pivot swings (Entry TF)
/// - BOS / CHoCH (trend-state aware)
/// - EQH / EQL liquidity pools (tolerance-based)
/// - M5 → M15 resampler (structure runs on M15 by default)
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
        if (candles.Count < 200)
            return new AnalysisResult(Array.Empty<Zone>(), Array.Empty<SignalEvent>());

        // Entry TF series:
        // - If input is M5, we do structure on M15 (default ICT-like).
        // - Otherwise, treat input as entry series.
        IReadOnlyList<Candle> entry = tf == Timeframes.M5
            ? Resampler.ToMinutes(candles, 15)
            : candles;

        if (entry.Count < 50)
            return new AnalysisResult(Array.Empty<Zone>(), Array.Empty<SignalEvent>());

        double tick = Structure.EstimateTick(entry);
        double tol = _p.EqTolerance > 0 ? _p.EqTolerance : 10.0 * tick; // default ~10 ticks

        var zones = new List<Zone>();
        var sigs = new List<SignalEvent>();

        // 1) Swings
        var swings = Structure.PivotSwings(entry, _p.SwingLenLtf);

        // Extract swing highs/lows sequences (for EQ clustering)
        double? lastHigh = null;
        DateTime lastHighT = default;
        double? lastLow = null;
        DateTime lastLowT = default;

        // Keep simple cluster memory to avoid spamming the same EQ zone
        double? lastEqhLevel = null;
        double? lastEqlLevel = null;

        // For BOS/CHoCH
        double? lastSwingHigh = null;
        double? lastSwingLow = null;
        int trendState = 0; // -1 down, +1 up

        // Create EQ zones from swing stream
        foreach (var s in swings)
        {
            if (s.IsHigh)
            {
                // update last swing high level for BOS
                lastSwingHigh = s.Price;

                if (lastHigh is not null && Math.Abs(s.Price - lastHigh.Value) <= tol)
                {
                    double level = (s.Price + lastHigh.Value) / 2.0;

                    if (lastEqhLevel is null || Math.Abs(level - lastEqhLevel.Value) > tol)
                    {
                        lastEqhLevel = level;

                        zones.Add(new Zone(
                            Id: Guid.NewGuid(),
                            Type: ZoneType.EQH,
                            Tf: Timeframes.M15,
                            Created: s.Time,
                            Expires: null,
                            Low: level - tol * 0.5,
                            High: level + tol * 0.5,
                            IsBullish: false,
                            Meta: $"EQH tol={tol:g}"
                        ));
                    }
                }

                lastHigh = s.Price;
                lastHighT = s.Time;
            }
            else
            {
                lastSwingLow = s.Price;

                if (lastLow is not null && Math.Abs(s.Price - lastLow.Value) <= tol)
                {
                    double level = (s.Price + lastLow.Value) / 2.0;

                    if (lastEqlLevel is null || Math.Abs(level - lastEqlLevel.Value) > tol)
                    {
                        lastEqlLevel = level;

                        zones.Add(new Zone(
                            Id: Guid.NewGuid(),
                            Type: ZoneType.EQL,
                            Tf: Timeframes.M15,
                            Created: s.Time,
                            Expires: null,
                            Low: level - tol * 0.5,
                            High: level + tol * 0.5,
                            IsBullish: true,
                            Meta: $"EQL tol={tol:g}"
                        ));
                    }
                }

                lastLow = s.Price;
                lastLowT = s.Time;
            }
        }

        // 2) BOS / CHoCH scan over entry bars
        // We use the most recently "confirmed" swing high/low from the pivot stream up to each bar.
        // For simplicity in v0.2: update lastSwingHigh/Low when we pass a swing index.
        int swingPtr = 0;
        double? curSwingHigh = null;
        double? curSwingLow = null;

        Zone? mostRecentEqh = zones.LastOrDefault(z => z.Type == ZoneType.EQH);
        Zone? mostRecentEql = zones.LastOrDefault(z => z.Type == ZoneType.EQL);

        for (int i = 0; i < entry.Count; i++)
        {
            // advance swing pointer
            while (swingPtr < swings.Count && swings[swingPtr].Index <= i)
            {
                var sp = swings[swingPtr];
                if (sp.IsHigh) curSwingHigh = sp.Price;
                else curSwingLow = sp.Price;
                swingPtr++;
            }

            if (curSwingHigh is null || curSwingLow is null) continue;

            var c = entry[i];
            bool bosUp = c.Close > curSwingHigh.Value;
            bool bosDn = c.Close < curSwingLow.Value;

            bool chochUp = false, chochDn = false;

            if (bosUp)
            {
                chochUp = trendState == -1;
                trendState = +1;
            }
            else if (bosDn)
            {
                chochDn = trendState == +1;
                trendState = -1;
            }

            if (!(bosUp || bosDn)) continue;

            // Minimal signal heuristic for v0.2:
            // - CHoCH_UP -> BUY (prefer if an EQL exists)
            // - CHoCH_DN -> SELL (prefer if an EQH exists)
            // - BOS without CHoCH emits no trade (but still could be logged later).
            if (chochUp)
            {
                var z = zones.LastOrDefault(z0 => z0.Type == ZoneType.EQL);
                sigs.Add(new SignalEvent(
                    Time: c.Time,
                    Side: Side.Buy,
                    ZoneId: z.Id == Guid.Empty ? null : z.Id,
                    EntryHint: c.Close,
                    SlHint: curSwingLow,      // structural invalidation hint
                    Tp1Hint: curSwingHigh,    // crude target hint
                    Tp2Hint: null,
                    Reasons: new[] { "CHoCH_UP", "BOS_UP", z.Id == Guid.Empty ? "NO_EQL" : "EQL_PRESENT" }
                ));
            }
            else if (chochDn)
            {
                var z = zones.LastOrDefault(z0 => z0.Type == ZoneType.EQH);
                sigs.Add(new SignalEvent(
                    Time: c.Time,
                    Side: Side.Sell,
                    ZoneId: z.Id == Guid.Empty ? null : z.Id,
                    EntryHint: c.Close,
                    SlHint: curSwingHigh,
                    Tp1Hint: curSwingLow,
                    Tp2Hint: null,
                    Reasons: new[] { "CHoCH_DN", "BOS_DN", z.Id == Guid.Empty ? "NO_EQH" : "EQH_PRESENT" }
                ));
            }
        }

        return new AnalysisResult(zones, sigs);
    }
}
