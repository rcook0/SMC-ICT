using System.Globalization;

namespace Axiom.Core;

/// <summary>
/// Minimal CSV IO for candles and exports.
/// Expected candle header: Time,Open,High,Low,Close,Volume
/// Time parses as ISO-8601 and normalizes to UTC.
/// </summary>
public static class CandleCsv
{
    public static List<Candle> Load(string path)
    {
        var candles = new List<Candle>();
        using var sr = new StreamReader(path);

        var header = sr.ReadLine();
        if (header is null) return candles;

        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');

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

    private static string CsvEscape(string? s)
    {
        if (s is null) return "";
        if (s.Contains('"')) s = s.Replace(""", """");
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return $""{s}"";
        return s;
    }

    public static void SaveSignalsCsv(string path, IReadOnlyList<SignalEvent> sigs)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("Time,Side,EntryHint,SlHint,Tp1Hint,Tp2Hint,Reasons,ZoneId");
        foreach (var s in sigs)
        {
            var reasons = string.Join('|', s.Reasons);
            sw.WriteLine($"{s.Time:o},{s.Side},{s.EntryHint},{s.SlHint},{s.Tp1Hint},{s.Tp2Hint},{CsvEscape(reasons)},{s.ZoneId}");
        }
    }

    public static void SaveZonesCsv(string path, IReadOnlyList<Zone> zones)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("Id,Type,Tf,Created,Expires,Low,High,IsBullish,Meta");
        foreach (var z in zones)
        {
            sw.WriteLine($"{z.Id},{z.Type},{z.Tf},{z.Created:o},{z.Expires:o},{z.Low},{z.High},{z.IsBullish},{CsvEscape(z.Meta)}");
        }
    }

    public static void SaveTradesCsv(string path, IReadOnlyList<TradeResult> trades)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("EntryTime,ExitTime,Side,Entry,Exit,InitialSL,Tp1,Tp2,ExitReason,RMultiple,ZoneId,Reasons");
        foreach (var t in trades)
        {
            sw.WriteLine($"{t.EntryTime:o},{t.ExitTime:o},{t.Side},{t.Entry},{t.Exit},{t.InitialSL},{t.Tp1},{t.Tp2},{t.ExitReason},{t.RMultiple},{t.ZoneId},{CsvEscape(string.Join('|', t.Reasons))}");
        }
    }
}
