using Axiom.Core;

namespace Axiom.SmcIct;

internal readonly record struct PriceLevel(double Price, string Tag);

internal static class TradeModel
{
    public static (double sl, double? tp1, double? tp2, IReadOnlyList<string> reasons) Compute(
        SmcIctParameters p,
        Side side,
        double entry,
        Zone? zone,
        double tick,
        double extHigh,
        double extLow,
        IReadOnlyList<PriceLevel> pools)
    {
        var reasons = new List<string>();

        // --- SL: zone invalidation + buffer
        double buf = Math.Max(tick, p.SlBufferTicks * tick);

        double sl;
        if (zone is not null)
        {
            sl = side == Side.Buy ? (zone.Value.Low - buf) : (zone.Value.High + buf);
            reasons.Add("SL_ZONE_INVALIDATION");
        }
        else
        {
            // fallback: dealing range extremes
            sl = side == Side.Buy ? (extLow - buf) : (extHigh + buf);
            reasons.Add("SL_RANGE_FALLBACK");
        }

        // --- Targets
        double risk = Math.Abs(entry - sl);
        if (risk <= 0) risk = tick;

        if (p.TpModel.Equals("RR", StringComparison.OrdinalIgnoreCase))
        {
            var tp = side == Side.Buy ? entry + p.DefaultRR * risk : entry - p.DefaultRR * risk;
            reasons.Add($"TP_RR_{p.DefaultRR:g}");
            return (sl, tp, p.EmitTp2 ? tp + (side==Side.Buy ? risk : -risk) : null, reasons);
        }

        // LiquidityPools: choose nearest pool beyond entry
        var candidates = pools
            .Where(x => side == Side.Buy ? x.Price > entry : x.Price < entry)
            .OrderBy(x => side == Side.Buy ? x.Price : -x.Price)
            .ToList();

        double? tp1 = null;
        double? tp2 = null;

        if (candidates.Count > 0)
        {
            tp1 = candidates[0].Price;
            reasons.Add($"TP1_{candidates[0].Tag}");
            if (p.EmitTp2 && candidates.Count > 1)
            {
                tp2 = candidates[1].Price;
                reasons.Add($"TP2_{candidates[1].Tag}");
            }
            else if (p.EmitTp2)
            {
                // fallback: extend by 1R
                tp2 = side == Side.Buy ? tp1 + risk : tp1 - risk;
                reasons.Add("TP2_FALLBACK_1R");
            }
        }
        else
        {
            // fallback to RR
            tp1 = side == Side.Buy ? entry + p.DefaultRR * risk : entry - p.DefaultRR * risk;
            reasons.Add("TP_FALLBACK_RR");
            if (p.EmitTp2) tp2 = side == Side.Buy ? tp1 + risk : tp1 - risk;
        }

        return (sl, tp1, tp2, reasons);
    }

    public static IReadOnlyList<PriceLevel> CollectPools(
        SmcIctParameters p,
        Side side,
        double entry,
        IReadOnlyList<Zone> zones,
        DateTime t,
        IReadOnlyDictionary<DateTime, (double High, double Low)> pdhPdl,
        double extHigh,
        double extLow)
    {
        var pools = new List<PriceLevel>();

        // External dealing range extremes (always useful)
        pools.Add(new PriceLevel(extHigh, "EXT_HIGH"));
        pools.Add(new PriceLevel(extLow, "EXT_LOW"));

        // EQ pools from zones (already clustered)
        foreach (var z in zones)
        {
            if (z.Type == ZoneType.EQH)
            {
                var lvl = (z.Low + z.High) / 2.0;
                pools.Add(new PriceLevel(lvl, "EQH"));
            }
            else if (z.Type == ZoneType.EQL)
            {
                var lvl = (z.Low + z.High) / 2.0;
                pools.Add(new PriceLevel(lvl, "EQL"));
            }
        }

        // PDH/PDL (previous day)
        if (p.UsePDH_PDL)
        {
            var day = Resampler.DayBucketUtc(t);
            var prev = day.AddDays(-1);
            if (pdhPdl.TryGetValue(prev, out var hl))
            {
                pools.Add(new PriceLevel(hl.High, "PDH"));
                pools.Add(new PriceLevel(hl.Low, "PDL"));
            }
        }

        // Scaffold for session hi/lo: disabled by default
        // (When enabled, implement per-session aggregation; kept out of v0.5 to avoid hidden assumptions.)

        return pools;
    }
}
