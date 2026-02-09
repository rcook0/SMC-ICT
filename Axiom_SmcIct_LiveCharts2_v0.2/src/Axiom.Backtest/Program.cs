using Axiom.Core;
using Axiom.SmcIct;
using System.Text.Json;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
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

Console.WriteLine($"Wrote: {outDir}/zones.json, signals.json, zones.csv, signals.csv");
