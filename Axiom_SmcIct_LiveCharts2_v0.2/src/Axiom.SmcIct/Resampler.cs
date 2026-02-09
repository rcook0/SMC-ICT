using Axiom.Core;

namespace Axiom.SmcIct;

internal static class Resampler
{
    /// <summary>
    /// Resample an input candle series to a fixed-minute timeframe.
    /// Buckets are aligned to UTC minute boundaries.
    /// </summary>
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
            var t = c.Time;
            var bucket = FloorToMinutes(t, minutes);

            if (cur is null || bucket != curBucket)
            {
                if (cur is not null)
                {
                    outBars.Add(cur.Value with { Volume = vol });
                }

                curBucket = bucket;
                vol = c.Volume;

                cur = new Candle(
                    Time: bucket,
                    Open: c.Open,
                    High: c.High,
                    Low: c.Low,
                    Close: c.Close,
                    Volume: c.Volume);
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

    private static DateTime FloorToMinutes(DateTime t, int minutes)
    {
        // Ensure UTC kind for stable bucketing
        if (t.Kind != DateTimeKind.Utc) t = DateTime.SpecifyKind(t, DateTimeKind.Utc);

        int m = t.Minute - (t.Minute % minutes);
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, m, 0, DateTimeKind.Utc);
    }
}
