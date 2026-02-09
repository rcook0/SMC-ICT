using Axiom.Core;
using Axiom.SmcIct;

namespace Axiom.Backtest;

public sealed record SweepRow(
    double DisplacementFactor,
    int InternalSwingLen,
    int ExternalSwingLen,
    bool RequireInducement,
    bool RestrictToKillzones,
    int Trades,
    double WinRate,
    double ProfitR,
    double MaxDrawdownR
);

public static class SweepRunner
{
    public static List<SweepRow> RunDefaultGrid(IReadOnlyList<Candle> candles)
    {
        var rows = new List<SweepRow>();

        var disp = new[] { 1.2, 1.5, 1.8 };
        var intLen = new[] { 3, 4, 5 };
        var extLen = new[] { 7, 9 };
        var induce = new[] { false, true };
        var kz = new[] { false, true };

        foreach (var d in disp)
        foreach (var il in intLen)
        foreach (var el in extLen)
        foreach (var ind in induce)
        foreach (var k in kz)
        {
            var p = new SmcIctParameters(
                DisplacementFactor: d,
                InternalSwingLen: il,
                ExternalSwingLen: el,
                RequireInducement: ind,
                RestrictToKillzones: k,
                KillzonePreset: "ICT_KZ_UTC",
                EntryModel: "Any"
            );

            var eng = new SmcIctEngine(p);
            var res = eng.Analyze(candles, tf: Timeframes.M5);

            var trades = TradeSimulator.Run(candles, res.Zones, res.Signals, new BacktestConfig());
            var summary = TradeSummary.Compute(trades);

            rows.Add(new SweepRow(
                DisplacementFactor: d,
                InternalSwingLen: il,
                ExternalSwingLen: el,
                RequireInducement: ind,
                RestrictToKillzones: k,
                Trades: trades.Count,
                WinRate: summary.WinRate,
                ProfitR: summary.TotalR,
                MaxDrawdownR: summary.MaxDrawdownR
            ));
        }

        return rows.OrderByDescending(r => r.ProfitR).ThenBy(r => r.MaxDrawdownR).ToList();
    }

    public static void SaveCsv(string path, IReadOnlyList<SweepRow> rows)
    {
        using var w = new StreamWriter(path);
        w.WriteLine("displacement_factor,internal_len,external_len,inducement,killzones,trades,winrate,profit_r,max_dd_r");
        foreach (var r in rows)
        {
            w.WriteLine($"{r.DisplacementFactor},{r.InternalSwingLen},{r.ExternalSwingLen},{r.RequireInducement},{r.RestrictToKillzones},{r.Trades},{r.WinRate},{r.ProfitR},{r.MaxDrawdownR}");
        }
    }
}
