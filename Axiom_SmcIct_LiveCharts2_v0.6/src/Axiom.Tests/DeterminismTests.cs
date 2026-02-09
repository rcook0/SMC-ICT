using System.Security.Cryptography;
using System.Text;
using Axiom.Core;
using Axiom.SmcIct;

namespace Axiom.Tests;

public sealed class DeterminismTests
{
    [Xunit.Fact]
    public void Analyze_Is_Deterministic()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_ohlcv.csv");
        var candles = CandleCsv.Load(path);

        var p = new SmcIctParameters(DeterministicSwings: true, EntryModel: "Any");
        var eng = new SmcIctEngine(p);

        var r1 = eng.Analyze(candles, tf: Timeframes.M5);
        var r2 = eng.Analyze(candles, tf: Timeframes.M5);

        var h1 = HashResult(r1);
        var h2 = HashResult(r2);

        Xunit.Assert.Equal(h1, h2);
    }

    [Xunit.Fact]
    public void Prefix_Run_Signals_Appear_In_Full_Run_No_Lookahead()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_ohlcv.csv");
        var candles = CandleCsv.Load(path);

        var p = new SmcIctParameters(DeterministicSwings: true, EntryModel: "Any");
        var eng = new SmcIctEngine(p);

        var full = eng.Analyze(candles, tf: Timeframes.M5);
        var fullKeys = full.Signals.Select(Key).ToHashSet();

        foreach (var cut in new[] { 600, 900, 1200, 1500 })
        {
            if (candles.Count <= cut) continue;
            var prefix = candles.Take(cut).ToList();
            var pref = eng.Analyze(prefix, tf: Timeframes.M5);

            foreach (var s in pref.Signals)
            {
                Xunit.Assert.Contains(Key(s), fullKeys);
            }
        }
    }

    private static string Key(SignalEvent s)
        => $"{s.Time:O}|{s.Side}|{s.ZoneId}|{Round(s.EntryHint)}|{Round(s.SlHint)}|{Round(s.Tp1Hint)}|{Round(s.Tp2Hint)}";

    private static string HashResult(AnalysisResult r)
    {
        var sb = new StringBuilder();

        foreach (var z in r.Zones
            .OrderBy(z => z.Type.ToString())
            .ThenBy(z => z.StartTime)
            .ThenBy(z => z.Low)
            .ThenBy(z => z.High))
        {
            sb.Append("Z|")
              .Append(z.Type).Append('|')
              .Append(z.StartTime.ToString("O")).Append('|')
              .Append(Round(z.Low)).Append('|')
              .Append(Round(z.High)).Append('|')
              .Append(z.IsBullish).Append('|')
              .Append(z.Meta ?? "")
              .AppendLine();
        }

        foreach (var s in r.Signals.OrderBy(s => s.Time).ThenBy(s => s.Side.ToString()))
        {
            sb.Append("S|")
              .Append(Key(s))
              .Append('|')
              .Append(string.Join(",", s.Reasons ?? Array.Empty<string>()))
              .AppendLine();
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private static string Round(double? x)
    {
        if (x is null) return "NA";
        // stable rounding for hashing
        return Math.Round(x.Value, 8).ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }
}
