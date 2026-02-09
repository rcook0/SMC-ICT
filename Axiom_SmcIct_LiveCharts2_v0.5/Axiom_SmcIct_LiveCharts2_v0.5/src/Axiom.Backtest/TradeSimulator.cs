using Axiom.Core;

namespace Axiom.Backtest;

public sealed record BacktestConfig(
    double PartialPct = 0.5,
    bool MoveSlToBE = true,
    int MaxHoldBars = 0 // 0 => disabled (uses base candle series)
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

        // Map time -> index (base timeframe)
        var idxOf = new Dictionary<DateTime, int>();
        for (int i = 0; i < baseCandles.Count; i++)
            idxOf[baseCandles[i].Time] = i;

        var trades = new List<TradeResult>();

        int iSig = 0;
        TradeState? pos = null;

        // Signals assumed sorted by time
        var sigs = signals.OrderBy(s => s.Time).ToList();

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
                    var entry = s.EntryHint ?? c.Close;

                    double sl;
                    if (s.SlHint is not null) sl = s.SlHint.Value;
                    else if (s.ZoneId is not null && zoneById.TryGetValue(s.ZoneId.Value, out var z))
                        sl = s.Side == Side.Buy ? z.Low : z.High;
                    else
                        sl = s.Side == Side.Buy ? c.Low : c.High;

                    // Basic sanity: SL must be beyond entry
                    if (s.Side == Side.Buy && sl >= entry) sl = Math.Min(sl, entry - 1e-9);
                    if (s.Side == Side.Sell && sl <= entry) sl = Math.Max(sl, entry + 1e-9);

                    pos = new TradeState(
                        entryTime: c.Time,
                        side: s.Side,
                        entry: entry,
                        initialSl: sl,
                        tp1: s.Tp1Hint,
                        tp2: s.Tp2Hint,
                        zoneId: s.ZoneId,
                        reasons: s.Reasons.ToList(),
                        entryIndex: i
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

        // Force close at end if still open
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
        public double Entry { get; }
        public double InitialSL { get; }
        public double SL { get; private set; }
        public double? Tp1 { get; }
        public double? Tp2 { get; }
        public Guid? ZoneId { get; }
        public List<string> Reasons { get; }

        private bool _tp1Hit;
        private double _realizedPnl; // price-units PnL for realized partials
        private double _remaining;   // fraction remaining (1.0 -> 0.0)

        public TradeState(DateTime entryTime, Side side, double entry, double initialSl,
                          double? tp1, double? tp2, Guid? zoneId, List<string> reasons, int entryIndex)
        {
            EntryTime = entryTime;
            EntryIndex = entryIndex;
            Side = side;
            Entry = entry;
            InitialSL = initialSl;
            SL = initialSl;
            Tp1 = tp1;
            Tp2 = tp2;
            ZoneId = zoneId;
            Reasons = reasons;

            _tp1Hit = false;
            _realizedPnl = 0;
            _remaining = 1.0;
        }

        public TradeResult? Update(Candle c, BacktestConfig cfg, int barsHeld)
        {
            // time stop
            if (cfg.MaxHoldBars > 0 && barsHeld >= cfg.MaxHoldBars)
                return Close(c, "TIME_STOP", c.Close);

            double risk = Math.Abs(Entry - InitialSL);
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

            // TP1 check (partial)
            if (!_tp1Hit && Tp1 is not null)
            {
                bool hit1 = Side == Side.Buy ? (c.High >= Tp1.Value) : (c.Low <= Tp1.Value);
                if (hit1)
                {
                    _tp1Hit = true;

                    var pct = Math.Clamp(cfg.PartialPct, 0.0, 1.0);
                    var fill = Tp1.Value;

                    _realizedPnl += pct * sign * (fill - Entry);
                    _remaining -= pct;

                    if (cfg.MoveSlToBE) SL = Entry;
                }
            }

            // TP2 / final exit after TP1
            if (_tp1Hit)
            {
                var finalTp = Tp2 ?? Tp1;
                if (finalTp is not null)
                {
                    bool hit2 = Side == Side.Buy ? (c.High >= finalTp.Value) : (c.Low <= finalTp.Value);
                    if (hit2)
                        return Close(c, "TP2", finalTp.Value);
                }
            }

            // If partialPct==1.0 and TP1 hit, allow full exit at TP1
            if (!_tp1Hit && Tp1 is not null && cfg.PartialPct >= 1.0)
            {
                bool hit = Side == Side.Buy ? (c.High >= Tp1.Value) : (c.Low <= Tp1.Value);
                if (hit) return Close(c, "TP1", Tp1.Value);
            }

            return null;
        }

        public TradeResult ForceClose(Candle c, string reason)
            => Close(c, reason, c.Close);

        private TradeResult Close(Candle c, string reason, double exitPrice)
        {
            double risk = Math.Abs(Entry - InitialSL);
            if (risk <= 0) risk = 1e-9;

            double sign = Side == Side.Buy ? +1.0 : -1.0;
            double unreal = _remaining * sign * (exitPrice - Entry);
            double pnl = _realizedPnl + unreal;

            double r = pnl / risk;

            return new TradeResult(EntryTime, c.Time, Side, Entry, exitPrice, InitialSL, Tp1, Tp2, reason, r, ZoneId, Reasons);
        }
    }
}
