using Axiom.Core;

namespace Axiom.SmcIct;

public sealed class SmcIctEngine
{
    private readonly SmcIctParameters _p;

    public SmcIctEngine(SmcIctParameters? parameters = null)
    {
        _p = parameters ?? new SmcIctParameters();
    }

    public AnalysisResult Analyze(IReadOnlyList<Candle> candles, string symbol = "SYMBOL", string tf = Timeframes.M5)
    {
        if (candles.Count < 400)
            return new AnalysisResult(Array.Empty<Zone>(), Array.Empty<SignalEvent>());

        // Entry TF: M15 when input is M5; otherwise input TF.
        IReadOnlyList<Candle> entry = tf == Timeframes.M5
            ? Resampler.ToMinutes(candles, 15)
            : candles;

        if (entry.Count < 120)
            return new AnalysisResult(Array.Empty<Zone>(), Array.Empty<SignalEvent>());

        // Micro confirm mapping (only for M5 input)
        Dictionary<DateTime, Candle>? microLastByEntryBucket = null;
        if (_p.RequireMicroConfirm && tf == Timeframes.M5)
        {
            microLastByEntryBucket = new Dictionary<DateTime, Candle>();
            foreach (var m in candles)
            {
                var b = Resampler.FloorToMinutesUtc(m.Time, 15);
                microLastByEntryBucket[b] = m; // last candle in bucket
            }
        }

        double tick = Structure.EstimateTick(entry);
        double eqTol = _p.EqTolerance > 0 ? _p.EqTolerance : 10.0 * tick;
        double sweepTol = Liquidity.DefaultSweepTol(tick, _p.SweepToleranceTicks);

        var zonesOut = new List<Zone>();
        var sigsOut  = new List<SignalEvent>();

        // -------- Structure streams (deterministic = confirmed swings)
        int intLen = Math.Max(2, _p.InternalSwingLen);
        int extLen = Math.Max(intLen + 2, _p.ExternalSwingLen);

        var internalSwings = Structure.PivotSwings(entry, intLen, deterministic: _p.DeterministicSwings);
        var externalSwings = Structure.PivotSwings(entry, extLen, deterministic: _p.DeterministicSwings);

        // last internal swing levels per bar (for sweeps) — based on confirmed availability
        var lastIntHigh = new double?[entry.Count];
        var lastIntLow  = new double?[entry.Count];
        {
            int ptr = 0;
            double? h = null, l = null;
            for (int i = 0; i < entry.Count; i++)
            {
                while (ptr < internalSwings.Count && internalSwings[ptr].ConfirmIndex <= i)
                {
                    var sp = internalSwings[ptr];
                    if (sp.IsHigh) h = sp.Price; else l = sp.Price;
                    ptr++;
                }
                lastIntHigh[i] = h;
                lastIntLow[i] = l;
            }
        }

        // -------- Daily high/low for PDH/PDL pools
        var byDay = new Dictionary<DateTime, (double High, double Low)>();
        foreach (var c in entry)
        {
            var d = Resampler.DayBucketUtc(c.Time);
            if (!byDay.TryGetValue(d, out var hl))
                byDay[d] = (c.High, c.Low);
            else
                byDay[d] = (Math.Max(hl.High, c.High), Math.Min(hl.Low, c.Low));
        }

        // -------- Weekly high/low for PWH/PWL pools
        var byWeek = new Dictionary<DateTime, (double High, double Low)>();
        foreach (var c in entry)
        {
            var w = Resampler.WeekBucketUtc(c.Time);
            if (!byWeek.TryGetValue(w, out var hl))
                byWeek[w] = (c.High, c.Low);
            else
                byWeek[w] = (Math.Max(hl.High, c.High), Math.Min(hl.Low, c.Low));
        }

        // -------- Session hi/lo pools (UTC preset windows)
        var bySession = new Dictionary<(DateTime Day, string Session), (double High, double Low)>();
        if (_p.UseSessionHiLo)
        {
            var sess = SessionPresets.Resolve(_p.SessionPreset);
            foreach (var c in entry)
            {
                var day = Resampler.DayBucketUtc(c.Time);
                int h = c.Time.Hour;
                foreach (var s in sess)
                {
                    if (h >= s.StartHourUtc && h < s.EndHourUtc)
                    {
                        var key = (day, s.Name);
                        if (!bySession.TryGetValue(key, out var hl))
                            bySession[key] = (c.High, c.Low);
                        else
                            bySession[key] = (Math.Max(hl.High, c.High), Math.Min(hl.Low, c.Low));
                    }
                }
            }
        }

        // -------- EQH/EQL pools from internal swings (clustered)
        double? lastHigh = null, lastLow = null;
        double? lastEqh = null, lastEql = null;

        double clusterMin = Math.Max(tick, _p.EqClusterMinSeparationTicks * tick);

        for (int k = 0; k < internalSwings.Count; k++)
        {
            var s = internalSwings[k];
            // only use when confirmed (avoid lookahead EQ pooling)
            // NOTE: we still store it globally and display it; it becomes actionable in TradeModel pools when beyond entry.
            if (s.IsHigh)
            {
                if (lastHigh is not null && Math.Abs(s.Price - lastHigh.Value) <= eqTol)
                {
                    var lvl = (s.Price + lastHigh.Value) / 2.0;
                    if (lastEqh is null || Math.Abs(lvl - lastEqh.Value) >= clusterMin)
                    {
                        lastEqh = lvl;
                        zonesOut.Add(new Zone(Guid.NewGuid(), ZoneType.EQH, Timeframes.M15, s.ConfirmTime, null,
                            lvl - eqTol * 0.5, lvl + eqTol * 0.5, false, $"EQH tol={eqTol:g}; status=Active"));
                    }
                }
                lastHigh = s.Price;
            }
            else
            {
                if (lastLow is not null && Math.Abs(s.Price - lastLow.Value) <= eqTol)
                {
                    var lvl = (s.Price + lastLow.Value) / 2.0;
                    if (lastEql is null || Math.Abs(lvl - lastEql.Value) >= clusterMin)
                    {
                        lastEql = lvl;
                        zonesOut.Add(new Zone(Guid.NewGuid(), ZoneType.EQL, Timeframes.M15, s.ConfirmTime, null,
                            lvl - eqTol * 0.5, lvl + eqTol * 0.5, true, $"EQL tol={eqTol:g}; status=Active"));
                    }
                }
                lastLow = s.Price;
            }
        }

        // -------- Displacement (body vs SMA(body))
        int smaLen = Math.Max(5, _p.DisplacementSmaLen);
        var body = new double[entry.Count];
        for (int i = 0; i < entry.Count; i++) body[i] = Math.Abs(entry[i].Close - entry[i].Open);

        var bodySma = new double[entry.Count];
        double sum = 0;
        for (int i = 0; i < entry.Count; i++)
        {
            sum += body[i];
            if (i >= smaLen) sum -= body[i - smaLen];
            bodySma[i] = i >= smaLen - 1 ? sum / smaLen : double.NaN;
        }

        bool IsDisplacement(int i)
        {
            if (i < smaLen - 1) return false;
            var s = bodySma[i];
            if (double.IsNaN(s) || s <= 0) return false;
            return body[i] > _p.DisplacementFactor * s;
        }

        // -------- External trend from external structure breaks (confirmed swings)
        int extPtr = 0;
        double? extHigh = null, extLow = null;
        int extTrend = 0;

        bool BreaksUp(Candle c, double level) => _p.UseWickForStructure ? (c.High > level) : (c.Close > level);
        bool BreaksDn(Candle c, double level) => _p.UseWickForStructure ? (c.Low < level) : (c.Close < level);

        int? lastSweepUp = null, lastSweepDn = null;

        var zoneStates = new List<ZoneState>();

        bool wantOB = _p.EntryModel is "Any" or "OB" or "OTE";
        bool wantFVG50 = _p.EntryModel is "Any" or "FVG50" or "OTE";
        bool wantBreaker = _p.EntryModel is "Any" or "Breaker" or "OTE";
        bool wantOTE = _p.EntryModel.Equals("OTE", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < entry.Count; i++)
        {
            // update ext swing refs up to i (confirmed availability)
            while (extPtr < externalSwings.Count && externalSwings[extPtr].ConfirmIndex <= i)
            {
                var sp = externalSwings[extPtr];
                if (sp.IsHigh) extHigh = sp.Price; else extLow = sp.Price;
                extPtr++;
            }

            var c = entry[i];

            // sweeps vs last internal swing
            var ih = lastIntHigh[i];
            var il = lastIntLow[i];
            bool sweepUp = ih is not null && Liquidity.IsSweepUp(c, ih.Value, sweepTol);
            bool sweepDn = il is not null && Liquidity.IsSweepDown(c, il.Value, sweepTol);
            if (sweepUp) lastSweepUp = i;
            if (sweepDn) lastSweepDn = i;

            UpdateZoneLifecycle(zoneStates, c, i, _p.ZoneMaxAgeBars);

            if (extHigh is null || extLow is null) continue;
            if (extHigh.Value <= extLow.Value) continue;

            bool bosUp = BreaksUp(c, extHigh.Value);
            bool bosDn = BreaksDn(c, extLow.Value);

            if (bosUp) extTrend = +1;
            else if (bosDn) extTrend = -1;

            bool disp = IsDisplacement(i);

            // Inducement gating: sweep against ext trend within lookback
            bool hasInducement = true;
            if (_p.RequireInducement && extTrend != 0)
            {
                int look = Math.Max(5, _p.InducementLookbackBars);
                if (extTrend == +1)
                    hasInducement = lastSweepDn is not null && (i - lastSweepDn.Value) <= look;
                else
                    hasInducement = lastSweepUp is not null && (i - lastSweepUp.Value) <= look;
            }

            // Create zones on displacement + BOS (directional)
            if (disp && (bosUp || bosDn) && hasInducement)
            {
                if (bosUp)
                {
                    bool ob = wantOB && TryCreateOb(entry, zoneStates, i, bullish: true, lookback: _p.ObLookbackBars, useBody: _p.ObUseBody);
                    bool fvg = TryCreateFvg(entry, zoneStates, i, bullish: true, requireDisp: _p.FvgRequireDisplacement, disp: disp);
                    if ((ob || fvg) && zoneStates.Count > 0) zoneStates[^1].SetInduced("disp+bos_bull");
                }
                else
                {
                    bool ob = wantOB && TryCreateOb(entry, zoneStates, i, bullish: false, lookback: _p.ObLookbackBars, useBody: _p.ObUseBody);
                    bool fvg = TryCreateFvg(entry, zoneStates, i, bullish: false, requireDisp: _p.FvgRequireDisplacement, disp: disp);
                    if ((ob || fvg) && zoneStates.Count > 0) zoneStates[^1].SetInduced("disp+bos_bear");
                }
            }

            MaybeCreateBreakers(zoneStates, c, i);

            // Emit entries
            EmitMitigationSignals(
                _p,
                zoneStates,
                sigsOut,
                c,
                extHigh.Value,
                extLow.Value,
                zonesOut.Concat(zoneStates.Select(zs => zs.Zone)).ToList(),
                byDay,
                byWeek,
                bySession,
                tick,
                wantOB,
                wantFVG50,
                wantBreaker,
                wantOTE,
                requireInduced: _p.RequireInducement,
                requireMicroConfirm: _p.RequireMicroConfirm,
                microLastByEntryBucket
            );
        }

        foreach (var zs in zoneStates)
        {
            zs.EnsureActiveMeta();
            zonesOut.Add(zs.Zone);
        }

        return new AnalysisResult(zonesOut, sigsOut);
    }

    // ---------------- Zone creation (dedup) ----------------
    private static bool OverlapsSimilar(Zone z, ZoneState existing)
    {
        if (existing.Status == ZoneStatus.Invalidated) return false;
        if (existing.Zone.Type != z.Type) return false;
        if (existing.Zone.IsBullish != z.IsBullish) return false;

        var midA = (existing.Zone.Low + existing.Zone.High) / 2.0;
        var midB = (z.Low + z.High) / 2.0;
        var widthA = existing.Zone.High - existing.Zone.Low;
        var widthB = z.High - z.Low;

        bool overlap = z.High >= existing.Zone.Low && z.Low <= existing.Zone.High;
        bool midClose = Math.Abs(midA - midB) <= (Math.Min(widthA, widthB) * 0.75 + 1e-9);
        return overlap && midClose;
    }

    private static bool TryCreateOb(IReadOnlyList<Candle> entry, List<ZoneState> zones, int i, bool bullish, int lookback, bool useBody)
    {
        int look = Math.Max(2, lookback);
        int start = Math.Max(0, i - look);

        for (int j = i - 1; j >= start; j--)
        {
            var cj = entry[j];
            bool isOpposite = bullish ? (cj.Close < cj.Open) : (cj.Close > cj.Open);
            if (!isOpposite) continue;

            double low, high;
            if (useBody)
            {
                if (bullish)
                {
                    low = cj.Low;
                    high = Math.Max(cj.Open, cj.Close);
                }
                else
                {
                    low = Math.Min(cj.Open, cj.Close);
                    high = cj.High;
                }
            }
            else
            {
                low = cj.Low;
                high = cj.High;
            }

            var z = new Zone(Guid.NewGuid(), ZoneType.OrderBlock, Timeframes.M15, cj.Time, null,
                low, high, bullish,
                $"OB; status=Active; src=last_opposite_candle; obUseBody={useBody}; createdIndex={j}");

            foreach (var ex in zones)
                if (OverlapsSimilar(z, ex)) return false;

            zones.Add(new ZoneState(z, createdIndex: j));
            return true;
        }
        return false;
    }

    private static bool TryCreateFvg(IReadOnlyList<Candle> entry, List<ZoneState> zones, int i, bool bullish, bool requireDisp, bool disp)
    {
        if (requireDisp && !disp) return false;
        if (i < 2) return false;

        var c0 = entry[i];
        var c2 = entry[i - 2];

        if (bullish)
        {
            if (c0.Low <= c2.High) return false;
            double low = c2.High;
            double high = c0.Low;

            var z = new Zone(Guid.NewGuid(), ZoneType.FairValueGap, Timeframes.M15, c0.Time, null,
                low, high, true, $"FVG; status=Active; src=gap_3c; mid={(low + high) / 2:g}");

            foreach (var ex in zones)
                if (OverlapsSimilar(z, ex)) return false;

            zones.Add(new ZoneState(z, createdIndex: i));
            return true;
        }
        else
        {
            if (c0.High >= c2.Low) return false;
            double low = c0.High;
            double high = c2.Low;

            var z = new Zone(Guid.NewGuid(), ZoneType.FairValueGap, Timeframes.M15, c0.Time, null,
                low, high, false, $"FVG; status=Active; src=gap_3c; mid={(low + high) / 2:g}");

            foreach (var ex in zones)
                if (OverlapsSimilar(z, ex)) return false;

            zones.Add(new ZoneState(z, createdIndex: i));
            return true;
        }
    }

    // ---------------- Lifecycle ----------------
    private static void UpdateZoneLifecycle(List<ZoneState> zones, Candle c, int barIndex, int maxAge)
    {
        foreach (var z in zones)
        {
            if (z.Status == ZoneStatus.Invalidated) continue;

            if (maxAge > 0 && (barIndex - z.CreatedIndex) > maxAge)
            {
                z.MarkInvalidated(c.Time);
                continue;
            }

            if (z.Zone.IsBullish)
            {
                if (c.Close < z.Zone.Low) { z.MarkInvalidated(c.Time); continue; }
            }
            else
            {
                if (c.Close > z.Zone.High) { z.MarkInvalidated(c.Time); continue; }
            }

            bool touched = c.High >= z.Zone.Low && c.Low <= z.Zone.High;
            if (touched) z.MarkMitigated(c.Time);
        }
    }

    // ---------------- Breakers ----------------
    private static void MaybeCreateBreakers(List<ZoneState> zones, Candle c, int barIndex)
    {
        foreach (var z in zones)
        {
            if (z.Zone.Type != ZoneType.OrderBlock) continue;
            if (z.Status != ZoneStatus.Invalidated) continue;
            if (z.BreakerSpawned) continue;

            var br = new Zone(Guid.NewGuid(), ZoneType.Breaker, z.Zone.Tf, c.Time, null,
                z.Zone.Low, z.Zone.High, !z.Zone.IsBullish,
                $"Breaker; status=Active; src=OB_invalidated; from={z.Zone.Id}");

            zones.Add(new ZoneState(br, createdIndex: barIndex));
            z.MarkBreakerSpawned();
        }
    }

    // ---------------- Signals + TradeModel integration ----------------
    private static void EmitMitigationSignals(
        SmcIctParameters p,
        List<ZoneState> zones,
        List<SignalEvent> sigsOut,
        Candle c,
        double extHigh,
        double extLow,
        IReadOnlyList<Zone> alreadyZones,
        IReadOnlyDictionary<DateTime, (double High, double Low)> byDay,
        IReadOnlyDictionary<DateTime, (double High, double Low)> byWeek,
        IReadOnlyDictionary<(DateTime Day, string Session), (double High, double Low)> bySession,
        double tick,
        bool wantOB,
        bool wantFVG50,
        bool wantBreaker,
        bool wantOTE,
        bool requireInduced,
        bool requireMicroConfirm,
        Dictionary<DateTime, Candle>? microLastByEntryBucket)
    {
        foreach (var z in zones)
        {
            if (z.FiredSignal) continue;
            if (z.Status != ZoneStatus.Mitigated) continue;

            if (requireInduced && !z.Induced) continue;

            if (z.Zone.Type == ZoneType.OrderBlock && !wantOB) continue;
            if (z.Zone.Type == ZoneType.Breaker && !wantBreaker) continue;

            if (z.Zone.Type == ZoneType.FairValueGap)
            {
                if (!wantFVG50) continue;
                var mid = (z.Zone.Low + z.Zone.High) / 2.0;
                bool accepted = z.Zone.IsBullish
                    ? (c.Low <= mid && c.Close >= mid)
                    : (c.High >= mid && c.Close <= mid);
                if (!accepted) continue;
            }

            if (requireMicroConfirm)
            {
                if (microLastByEntryBucket is null) continue;
                if (!microLastByEntryBucket.TryGetValue(c.Time, out var m)) continue;
                bool microOk = z.Zone.IsBullish ? (m.Close > m.Open) : (m.Close < m.Open);
                if (!microOk) continue;
            }

            // Entry price chosen as close on mitigation bar (deterministic)
            var side = z.Zone.IsBullish ? Side.Buy : Side.Sell;
            var entry = c.Close;

            // Discount/Premium filter (optional)
            if (p.RequireDiscountPremium)
            {
                var mid = (extHigh + extLow) / 2.0;
                if (side == Side.Buy && entry > mid) continue;
                if (side == Side.Sell && entry < mid) continue;
            }

            // OTE filter (if enabled)
            if (wantOTE)
            {
                var rng = extHigh - extLow;
                if (rng <= 0) continue;

                if (side == Side.Buy)
                {
                    var oteLo = extHigh - p.OteHigh * rng; // 0.79
                    var oteHi = extHigh - p.OteLow * rng;  // 0.62
                    if (entry < oteLo || entry > oteHi) continue;
                }
                else
                {
                    var oteLo = extLow + p.OteLow * rng;   // 0.62
                    var oteHi = extLow + p.OteHigh * rng;  // 0.79
                    if (entry < oteLo || entry > oteHi) continue;
                }
            }

            // Trade model: pools + SL/TP
            var pools = TradeModel.CollectPools(p, side, entry, alreadyZones, c.Time, byDay, byWeek, bySession, extHigh, extLow);
            var (sl, tp1, tp2, tmReasons) = TradeModel.Compute(p, side, entry, z.Zone, tick, extHigh, extLow, pools);

            var reasons = new List<string>
            {
                "MITIGATION",
                z.Zone.Type.ToString().ToUpperInvariant(),
                z.Zone.IsBullish ? "BIAS_BULL" : "BIAS_BEAR",
            };

            if (requireInduced) reasons.Add("INDUCEMENT_REQUIRED");
            if (z.Induced) reasons.Add("INDUCED");
            if (requireMicroConfirm) reasons.Add("MICRO_CONFIRM");
            if (wantOTE) reasons.Add("OTE_FILTER");

            reasons.AddRange(tmReasons);

            sigsOut.Add(new SignalEvent(c.Time, side, z.Zone.Id, entry, sl, tp1, tp2, reasons));
            z.FiredSignal = true;
        }
    }
}
