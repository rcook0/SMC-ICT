namespace Axiom.SmcIct;

internal readonly record struct SessionWindow(string Name, int StartHourUtc, int EndHourUtc);

internal static class SessionPresets
{
    public static IReadOnlyList<SessionWindow> Resolve(string preset)
    {
        preset = (preset ?? "").Trim().ToUpperInvariant();
        return preset switch
        {
            "FX_UTC" => new[]
            {
                new SessionWindow("ASIA", 0, 6),
                new SessionWindow("LONDON", 7, 11),
                new SessionWindow("NY", 13, 17),
            },
            _ => new[] // ICT_UTC (default)
            {
                new SessionWindow("ASIA", 0, 6),
                new SessionWindow("LONDON", 7, 11),
                new SessionWindow("NY", 13, 17),
            }
        };
    }
}
