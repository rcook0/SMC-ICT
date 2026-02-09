namespace Axiom.SmcIct;

internal readonly record struct KillzoneWindow(string Name, int StartHourUtc, int EndHourUtc);

internal static class KillzonePresets
{
    public static IReadOnlyList<KillzoneWindow> Resolve(string preset)
    {
        preset = (preset ?? "").Trim().ToUpperInvariant();
        return preset switch
        {
            // UTC approximations
            "ICT_KZ_UTC" => new[]
            {
                new KillzoneWindow("LONDON_KZ", 7, 10),
                new KillzoneWindow("NY_KZ", 12, 15),
            },
            _ => new[]
            {
                new KillzoneWindow("LONDON_KZ", 7, 10),
                new KillzoneWindow("NY_KZ", 12, 15),
            }
        };
    }

    public static bool IsInKillzoneUtc(DateTime tUtc, string preset)
    {
        if (tUtc.Kind != DateTimeKind.Utc) tUtc = DateTime.SpecifyKind(tUtc, DateTimeKind.Utc);
        int h = tUtc.Hour;
        foreach (var kz in Resolve(preset))
            if (h >= kz.StartHourUtc && h < kz.EndHourUtc)
                return true;
        return false;
    }
}
