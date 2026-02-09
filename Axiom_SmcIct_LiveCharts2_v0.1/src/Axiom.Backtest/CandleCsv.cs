using Axiom.Core;
using System.Globalization;

namespace Axiom.Backtest;

public static class CandleCsv
{
    public static List<Candle> Load(string path)
    {
        var candles = new List<Candle>();
        using var sr = new StreamReader(path);

        string? header = sr.ReadLine();
        if (header is null) return candles;

        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');

            // Time,Open,High,Low,Close,Volume
            var t = DateTime.Parse(parts[0], null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            double o = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double h = double.Parse(parts[2], CultureInfo.InvariantCulture);
            double l = double.Parse(parts[3], CultureInfo.InvariantCulture);
            double c = double.Parse(parts[4], CultureInfo.InvariantCulture);
            double v = parts.Length > 5 ? double.Parse(parts[5], CultureInfo.InvariantCulture) : 0;

            candles.Add(new Candle(t, o, h, l, c, v));
        }

        return candles;
    }

    public static void SaveSignalsCsv(string path, IReadOnlyList<Axiom.Core.SignalEvent> sigs)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("Time,Side,EntryHint,SlHint,Tp1Hint,Tp2Hint,Reasons,ZoneId");
        foreach (var s in sigs)
        {
            sw.WriteLine($"{s.Time:o},{s.Side},{s.EntryHint},{s.SlHint},{s.Tp1Hint},{s.Tp2Hint},"{string.Join('|', s.Reasons)}",{s.ZoneId}");
        }
    }

    public static void SaveZonesCsv(string path, IReadOnlyList<Axiom.Core.Zone> zones)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("Id,Type,Tf,Created,Expires,Low,High,IsBullish,Meta");
        foreach (var z in zones)
        {
            sw.WriteLine($"{z.Id},{z.Type},{z.Tf},{z.Created:o},{z.Expires:o},{z.Low},{z.High},{z.IsBullish},"{z.Meta}"");
        }
    }
}
