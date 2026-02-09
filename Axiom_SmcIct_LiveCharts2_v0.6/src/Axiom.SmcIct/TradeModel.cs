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
            return (sl, tp, p.EmitTp2 ? tp + (side == Side.Buy ? risk : -risk) : null, reasons);
        }

        // LiquidityPools: choose nearest pools beyond entry
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
                tp2 = side == Side.Buy ? tp1 + risk : tp1 - risk;
                reasons.Add("TP2_FALLBACK_1R");
            }
        }
        else
        {
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
        IReadOnlyDictionary<DateTime, (double High, double Low)> byDay,
        IReadOnlyDictionary<DateTime, (double High, double Low)> byWeek,
        IReadOnlyDictionary<(DateTime Day, string Session), (double High, double Low)> bySession,
        double extHigh,
        double extLow)
    {
        var pools = new List<PriceLevel>();

        // External dealing range extremes
        if (p.UseExtRange)
        {
            pools.Add(new PriceLevel(extHigh, "EXT_HIGH"));
            pools.Add(new PriceLevel(extLow, "EXT_LOW"));
        }

        // EQ pools from zones
        if (p.UseEqPools)
        {
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
        }

        // PDH/PDL (previous day)
        if (p.UsePDH_PDL)
        {
            var day = Resampler.DayBucketUtc(t);
            var prev = day.AddDays(-1);
            if (byDay.TryGetValue(prev, out var hl))
            {
                pools.Add(new PriceLevel(hl.High, "PDH"));
                pools.Add(new PriceLevel(hl.Low, "PDL"));
            }
        }

        // PWH/PWL (previous week)
        if (p.UsePWH_PWL)
        {
            var wk = Resampler.WeekBucketUtc(t);
            var prevWk = wk.AddDays(-7);
            if (byWeek.TryGetValue(prevWk, out var hl))
            {
                pools.Add(new PriceLevel(hl.High, "PWH"));
                pools.Add(new PriceLevel(hl.Low, "PWL"));
            }
        }

        // Session hi/lo (most recent completed day’s sessions by default)
        if (p.UseSessionHiLo)
        {
            var day = Resampler.DayBucketUtc(t);
            // prefer same day if exists, else previous day
            foreach (var key in new[] { day, day.AddDays(-1) })
            {
                foreach (var s in SessionPresets.Resolve(p.SessionPreset))
                {
                    if (bySession.TryGetValue((key, s.Name), out var hl))
                    {
                        pools.Add(new PriceLevel(hl.High, $"{s.Name}_H"));
                        pools.Add(new PriceLevel(hl.Low, $"{s.Name}_L"));
                    }
                }

                // if we found any sessions for that day, stop searching
                if (pools.Any(x => x.Tag.Contains("_H") || x.Tag.Contains("_L"))) break;
            }
        }

        return pools;
    }
}
