using Axiom.Backtest;
using Axiom.Core;
using Axiom.SmcIct;
using System.Text.Json;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static int GetIntArg(string[] args, string name, int def)
{
    var v = GetArg(args, name);
    return v is null ? def : int.Parse(v);
}

static double GetDoubleArg(string[] args, string name, double def)
{
    var v = GetArg(args, name);
    return v is null ? def : double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
}

var csv = GetArg(args, "--csv") ?? Path.Combine("data", "sample_ohlcv.csv");
var outDir = GetArg(args, "--out") ?? "out";
Directory.CreateDirectory(outDir);

var mode = (GetArg(args, "--mode") ?? "run").Trim().ToLowerInvariant(); // run | sweep

Console.WriteLine($"Loading candles: {csv}");
var candles = CandleCsv.Load(csv);
Console.WriteLine($"Candles: {candles.Count}");

if (mode == "sweep")
{
    Console.WriteLine("Running default sweep grid...");
    var rows = SweepRunner.RunDefaultGrid(candles);
    SweepRunner.SaveCsv(Path.Combine(outDir, "sweep.csv"), rows);

    var best = rows.FirstOrDefault();
    Console.WriteLine($"Sweep rows: {rows.Count}");
    Console.WriteLine($"Best: disp={best.DisplacementFactor:g} int={best.InternalSwingLen} ext={best.ExternalSwingLen} ind={best.RequireInducement} kz={best.RestrictToKillzones} profitR={best.ProfitR:g} maxDD={best.MaxDrawdownR:g}");
    Console.WriteLine($"Wrote: {outDir}/sweep.csv");
    return;
}

var engine = new SmcIctEngine(new SmcIctParameters());
var res = engine.Analyze(candles, symbol: Path.GetFileNameWithoutExtension(csv), tf: Timeframes.M5);

Console.WriteLine($"Zones:   {res.Zones.Count}");
Console.WriteLine($"Signals: {res.Signals.Count}");

File.WriteAllText(Path.Combine(outDir, "zones.json"), JsonSerializer.Serialize(res.Zones, JsonUtil.Options));
File.WriteAllText(Path.Combine(outDir, "signals.json"), JsonSerializer.Serialize(res.Signals, JsonUtil.Options));

CandleCsv.SaveZonesCsv(Path.Combine(outDir, "zones.csv"), res.Zones);
CandleCsv.SaveSignalsCsv(Path.Combine(outDir, "signals.csv"), res.Signals);

// --- Trade simulation (+ economics)
var partialPct = GetDoubleArg(args, "--partial", 0.5);
var moveBe = GetIntArg(args, "--be", 1) == 1;
var maxHoldBars = GetIntArg(args, "--max_hold_bars", 0);

// economics args
var econoMode = GetArg(args, "--econo_mode") ?? "Fixed";
var atrLen = GetIntArg(args, "--atr_len", 14);
var atrSpreadFrac = GetDoubleArg(args, "--atr_spread_frac", 0.0);
var atrSlipFrac = GetDoubleArg(args, "--atr_slip_frac", 0.0);

var spreadTicks = GetDoubleArg(args, "--spread_ticks", 0.0);
var spreadBps   = GetDoubleArg(args, "--spread_bps", 0.0);
var slipTicks   = GetDoubleArg(args, "--slip_ticks", 0.0);
var slipBps     = GetDoubleArg(args, "--slip_bps", 0.0);
var commBps     = GetDoubleArg(args, "--comm_bps", 0.0);
var commFixed   = GetDoubleArg(args, "--comm_fixed", 0.0);
var qty         = GetDoubleArg(args, "--qty", 1.0);

var econ = new EconomicsConfig(
    SpreadTicks: spreadTicks,
    SpreadBps: spreadBps,
    SlippageTicks: slipTicks,
    SlippageBps: slipBps,
    CommissionBps: commBps,
    CommissionFixed: commFixed,
    Quantity: qty,
    Mode: econoMode,
    AtrLen: atrLen,
    AtrSpreadFrac: atrSpreadFrac,
    AtrSlipFrac: atrSlipFrac
);

var cfg = new BacktestConfig(PartialPct: partialPct, MoveSlToBE: moveBe, MaxHoldBars: maxHoldBars, Econ: econ);
var trades = TradeSimulator.Run(candles, res.Zones, res.Signals, cfg);

File.WriteAllText(Path.Combine(outDir, "trades.json"), JsonSerializer.Serialize(trades, JsonUtil.Options));
CandleCsv.SaveTradesCsv(Path.Combine(outDir, "trades.csv"), trades);

// Summary stats
var summary = TradeSummary.Compute(trades);
File.WriteAllText(Path.Combine(outDir, "summary.json"), JsonSerializer.Serialize(summary, JsonUtil.Options));

// Equity curve report
var eq = ReportPack.EquityCurveR(trades);
ReportPack.SaveEquityCsv(Path.Combine(outDir, "equity_curve.csv"), eq);

Console.WriteLine($"Trades:  {trades.Count}");
Console.WriteLine($"Wrote: {outDir}/zones.json, signals.json, trades.json, summary.json (+ csv)");
Console.WriteLine($"Econ: mode={econoMode} spread_ticks={spreadTicks:g} spread_bps={spreadBps:g} slip_ticks={slipTicks:g} slip_bps={slipBps:g} comm_bps={commBps:g} comm_fixed={commFixed:g} qty={qty:g} atr_len={atrLen} atr_spread_frac={atrSpreadFrac:g} atr_slip_frac={atrSlipFrac:g}");
