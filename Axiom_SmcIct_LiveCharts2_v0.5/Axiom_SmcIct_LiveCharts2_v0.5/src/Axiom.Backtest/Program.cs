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

Console.WriteLine($"Loading candles: {csv}");
var candles = CandleCsv.Load(csv);
Console.WriteLine($"Candles: {candles.Count}");

var engine = new SmcIctEngine(new SmcIctParameters());
var res = engine.Analyze(candles, symbol: Path.GetFileNameWithoutExtension(csv), tf: Timeframes.M5);

Console.WriteLine($"Zones:   {res.Zones.Count}");
Console.WriteLine($"Signals: {res.Signals.Count}");

File.WriteAllText(Path.Combine(outDir, "zones.json"),   JsonSerializer.Serialize(res.Zones,   JsonUtil.Options));
File.WriteAllText(Path.Combine(outDir, "signals.json"), JsonSerializer.Serialize(res.Signals, JsonUtil.Options));

CandleCsv.SaveZonesCsv(Path.Combine(outDir, "zones.csv"), res.Zones);
CandleCsv.SaveSignalsCsv(Path.Combine(outDir, "signals.csv"), res.Signals);

// --- v0.5: trade simulation
var partialPct = GetDoubleArg(args, "--partial", 0.5);
var moveBe = GetIntArg(args, "--be", 1) == 1;
var maxHoldBars = GetIntArg(args, "--max_hold_bars", 0);

var cfg = new BacktestConfig(PartialPct: partialPct, MoveSlToBE: moveBe, MaxHoldBars: maxHoldBars);
var trades = TradeSimulator.Run(candles, res.Zones, res.Signals, cfg);

File.WriteAllText(Path.Combine(outDir, "trades.json"), JsonSerializer.Serialize(trades, JsonUtil.Options));
CandleCsv.SaveTradesCsv(Path.Combine(outDir, "trades.csv"), trades);

// Summary stats
var summary = TradeSummary.Compute(trades);
File.WriteAllText(Path.Combine(outDir, "summary.json"), JsonSerializer.Serialize(summary, JsonUtil.Options));

Console.WriteLine($"Trades:  {trades.Count}");
Console.WriteLine($"Wrote: {outDir}/zones.json, signals.json, trades.json, summary.json (+ csv)");
