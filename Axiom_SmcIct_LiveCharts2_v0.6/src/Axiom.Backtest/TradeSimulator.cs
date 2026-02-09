using Axiom.Core;

namespace Axiom.Backtest;

public sealed record BacktestConfig(
    double PartialPct = 0.5,
    bool MoveSlToBE = true,
    int MaxHoldBars = 0,     // 0 => disabled (uses base candle series)
    EconomicsConfig? Econ = null
);

public static class TradeSimulator
{
    public static List<TradeResult> Run(
        IReadOnlyList<Candle> baseCandles,
        IReadOnlyList<Zone> zones,
        IReadOnlyList<SignalEvent> signals,
        BacktestConfig cfg)
    {
        var zoneById = zones.ToDictionary(z => z.Id, z => z);

        var trades = new List<TradeResult>();

        int iSig = 0;
        TradeState? pos = null;

        var sigs = signals.OrderBy(s => s.Time).ToList();

        double tick = Economics.TickEstimate(baseCandles);
        var econ = cfg.Econ ?? new EconomicsConfig();

        for (int i = 0; i < baseCandles.Count; i++)
        {
            var c = baseCandles[i];

            // Open a new position if flat and signal aligns on this candle timestamp
            if (pos is null)
            {
                while (iSig < sigs.Count && sigs[iSig].Time < c.Time) iSig++;

                if (iSig < sigs.Count && sigs[iSig].Time == c.Time)
                {
                    var s = sigs[iSig];
                    var midEntry = s.EntryHint ?? c.Close;

                    double sl;
                    if (s.SlHint is not null) sl = s.SlHint.Value;
                    else if (s.ZoneId is not null && zoneById.TryGetValue(s.ZoneId.Value, out var z))
                        sl = s.Side == Side.Buy ? z.Low : z.High;
                    else
                        sl = s.Side == Side.Buy ? c.Low : c.High;

                    // Basic sanity: SL must be beyond entry
                    if (s.Side == Side.Buy && sl >= midEntry) sl = Math.Min(sl, midEntry - 1e-9);
                    if (s.Side == Side.Sell && sl <= midEntry) sl = Math.Max(sl, midEntry + 1e-9);

                    var entryFill = Economics.FillEntry(midEntry, s.Side, tick, econ);
                    var entryComm = Economics.CommissionPerSide(entryFill, econ);

                    pos = new TradeState(
                        entryTime: c.Time,
                        side: s.Side,
                        entryMid: midEntry,
                        entryFill: entryFill,
                        entryCommission: entryComm,
                        initialSl: sl,
                        tp1: s.Tp1Hint,
                        tp2: s.Tp2Hint,
                        zoneId: s.ZoneId,
                        reasons: s.Reasons.ToList(),
                        entryIndex: i,
                        tick: tick,
                        econ: econ
                    );
                }
            }

            if (pos is not null)
            {
                int barsHeld = i - pos.EntryIndex;
                var exit = pos.Update(c, cfg, barsHeld);
                if (exit is not null)
                {
                    trades.Add(exit.Value);
                    pos = null;
                }
            }
        }

        if (pos is not null)
        {
            var last = baseCandles[^1];
            trades.Add(pos.ForceClose(last, "EOD"));
        }

        return trades;
    }

    private sealed class TradeState
    {
        public DateTime EntryTime { get; }
        public int EntryIndex { get; }
        public Side Side { get; }

        public double EntryMid { get; }
        public double EntryFill { get; }
        public double InitialSL { get; }
        public double SL { get; private set; }
        public double? Tp1 { get; }
        public double? Tp2 { get; }
        public Guid? ZoneId { get; }
        public List<string> Reasons { get; }

        private readonly double _tick;
        private readonly EconomicsConfig _econ;

        private bool _tp1Hit;
        private double _realizedPnl; // net price-units PnL for realized partials (incl costs)
        private double _remaining;   // fraction remaining
        private double _costs;       // accumulated costs in currency (price*qty units)

        public TradeState(
            DateTime entryTime,
            Side side,
            double entryMid,
            double entryFill,
            double entryCommission,
            double initialSl,
            double? tp1,
            double? tp2,
            Guid? zoneId,
            List<string> reasons,
            int entryIndex,
            double tick,
            EconomicsConfig econ)
        {
            EntryTime = entryTime;
            EntryIndex = entryIndex;
            Side = side;

            EntryMid = entryMid;
            EntryFill = entryFill;

            InitialSL = initialSl;
            SL = initialSl;
            Tp1 = tp1;
            Tp2 = tp2;
            ZoneId = zoneId;
            Reasons = reasons;

            _tick = tick;
            _econ = econ;

            _tp1Hit = false;
            _realizedPnl = 0;
            _remaining = 1.0;

            _costs = entryCommission;
        }

        public TradeResult? Update(Candle c, BacktestConfig cfg, int barsHeld)
        {
            if (cfg.MaxHoldBars > 0 && barsHeld >= cfg.MaxHoldBars)
                return Close(c, "TIME_STOP", c.Close);

            double risk = Math.Abs(EntryFill - InitialSL);
            if (risk <= 0) risk = 1e-9;

            double sign = Side == Side.Buy ? +1.0 : -1.0;

            // STOP check
            if (Side == Side.Buy)
            {
                if (c.Low <= SL) return Close(c, _tp1Hit ? "BE_STOP" : "STOP", SL);
            }
            else
            {
                if (c.High >= SL) return Close(c, _tp1Hit ? "BE_STOP" : "STOP", SL);
            }

            // TP1
            if (!_tp1Hit && Tp1 is not null)
            {
                if (Side == Side.Buy && c.High >= Tp1.Value)
                {
                    _tp1Hit = true;
                    double fill = Economics.FillExit(Tp1.Value, Side, _tick, _econ);
                    double comm = Economics.CommissionPerSide(fill, _econ) * cfg.PartialPct;
                    _costs += comm;

                    double qty = _econ.Quantity * cfg.PartialPct;
                    double pnl = qty * sign * (fill - EntryFill);
                    _realizedPnl += pnl;

                    _remaining = 1.0 - cfg.PartialPct;

                    if (cfg.MoveSlToBE)
                        SL = EntryMid; // BE at mid-entry, still subject to economics on actual fill

                    return null;
                }

                if (Side == Side.Sell && c.Low <= Tp1.Value)
                {
                    _tp1Hit = true;
                    double fill = Economics.FillExit(Tp1.Value, Side, _tick, _econ);
                    double comm = Economics.CommissionPerSide(fill, _econ) * cfg.PartialPct;
                    _costs += comm;

                    double qty = _econ.Quantity * cfg.PartialPct;
                    double pnl = qty * sign * (fill - EntryFill);
                    _realizedPnl += pnl;

                    _remaining = 1.0 - cfg.PartialPct;

                    if (cfg.MoveSlToBE)
                        SL = EntryMid;

                    return null;
                }
            }

            // TP2
            if (Tp2 is not null)
            {
                if (Side == Side.Buy && c.High >= Tp2.Value)
                    return Close(c, "TP2", Tp2.Value);

                if (Side == Side.Sell && c.Low <= Tp2.Value)
                    return Close(c, "TP2", Tp2.Value);
            }

            return null;
        }

        public TradeResult ForceClose(Candle c, string reason)
            => Close(c, reason, c.Close);

        private TradeResult Close(Candle c, string reason, double exitPriceMid)
        {
            double risk = Math.Abs(EntryFill - InitialSL);
            if (risk <= 0) risk = 1e-9;

            double sign = Side == Side.Buy ? +1.0 : -1.0;

            // apply economics on exit for remaining size
            double fill = Economics.FillExit(exitPriceMid, Side, _tick, _econ);
            double comm = Economics.CommissionPerSide(fill, _econ) * _remaining;
            _costs += comm;

            double qty = _econ.Quantity * _remaining;
            double unreal = qty * sign * (fill - EntryFill);
            double pnl = _realizedPnl + unreal - _costs; // net after commissions

            double r = pnl / (risk * _econ.Quantity);

            return new TradeResult(
                EntryTime,
                c.Time,
                Side,
                EntryFill,
                fill,
                InitialSL,
                Tp1,
                Tp2,
                reason,
                r,
                ZoneId,
                Reasons);
        }
    }
}
