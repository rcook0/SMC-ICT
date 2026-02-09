using Axiom.Core;

namespace Axiom.SmcIct;

internal static class Resampler
{
    public static List<Candle> ToMinutes(IReadOnlyList<Candle> src, int minutes)
    {
        if (src.Count == 0) return new List<Candle>();
        if (minutes <= 1) return src.ToList();

        var outBars = new List<Candle>();
        Candle? cur = null;
        DateTime curBucket = default;
        double vol = 0;

        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            var bucket = FloorToMinutesUtc(c.Time, minutes);

            if (cur is null || bucket != curBucket)
            {
                if (cur is not null) outBars.Add(cur.Value with { Volume = vol });

                curBucket = bucket;
                vol = c.Volume;
                cur = new Candle(bucket, c.Open, c.High, c.Low, c.Close, c.Volume);
            }
            else
            {
                vol += c.Volume;
                cur = cur.Value with
                {
                    High = Math.Max(cur.Value.High, c.High),
                    Low = Math.Min(cur.Value.Low, c.Low),
                    Close = c.Close,
                };
            }
        }

        if (cur is not null) outBars.Add(cur.Value with { Volume = vol });
        return outBars;
    }

    internal static DateTime FloorToMinutesUtc(DateTime t, int minutes)
    {
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        int m = t.Minute - (t.Minute % minutes);
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, m, 0, DateTimeKind.Utc);
    }

    internal static DateTime DayBucketUtc(DateTime t)
    {
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);
        return new DateTime(t.Year, t.Month, t.Day, 0, 0, 0, DateTimeKind.Utc);
    }
}
