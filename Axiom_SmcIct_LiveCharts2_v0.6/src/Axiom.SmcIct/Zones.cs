using Axiom.Core;

namespace Axiom.SmcIct;

internal enum ZoneStatus { Active, Mitigated, Invalidated }

internal sealed class ZoneState
{
    public ZoneState(Zone zone, int createdIndex)
    {
        Zone = zone;
        CreatedIndex = createdIndex;
        Status = ZoneStatus.Active;
        FiredSignal = false;
        BreakerSpawned = false;
        Induced = false;
        CreatedBy = "";
    }

    public Zone Zone { get; private set; }
    public int CreatedIndex { get; }
    public ZoneStatus Status { get; private set; }
    public DateTime? MitigatedAt { get; private set; }
    public DateTime? InvalidatedAt { get; private set; }

    public bool FiredSignal { get; set; }
    public bool BreakerSpawned { get; private set; }

    public bool Induced { get; private set; }
    public string CreatedBy { get; private set; }

    public void SetInduced(string createdBy)
    {
        Induced = true;
        CreatedBy = createdBy;
        var meta = Zone.Meta ?? "";
        if (!meta.Contains("induced="))
            Zone = Zone with { Meta = (meta.Length > 0 ? meta + "; " : "") + $"induced=true; createdBy={createdBy}" };
    }

    public void MarkMitigated(DateTime t)
    {
        if (Status != ZoneStatus.Active) return;
        Status = ZoneStatus.Mitigated;
        MitigatedAt = t;
        Zone = Zone with { Meta = MetaWithStatus(Zone.Meta, "Mitigated", t) };
    }

    public void MarkInvalidated(DateTime t)
    {
        if (Status == ZoneStatus.Invalidated) return;
        Status = ZoneStatus.Invalidated;
        InvalidatedAt = t;
        Zone = Zone with { Meta = MetaWithStatus(Zone.Meta, "Invalidated", t) };
    }

    public void MarkBreakerSpawned()
    {
        if (BreakerSpawned) return;
        BreakerSpawned = true;
        var meta = Zone.Meta ?? "";
        if (!meta.Contains("breakerSpawned"))
            Zone = Zone with { Meta = (meta.Length > 0 ? meta + "; " : "") + "breakerSpawned=true" };
    }

    public void EnsureActiveMeta()
    {
        if (Zone.Meta is null || !Zone.Meta.Contains("status="))
            Zone = Zone with { Meta = MetaWithStatus(Zone.Meta, "Active", null) };
    }

    private static string MetaWithStatus(string? meta, string status, DateTime? t)
    {
        var baseMeta = meta ?? "";
        baseMeta = baseMeta.Replace("status=Active", "").Replace("status=Mitigated", "").Replace("status=Invalidated", "");
        baseMeta = baseMeta.Replace(";;", ";").Trim().Trim(';').Trim();

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseMeta)) parts.Add(baseMeta);

        parts.Add($"status={status}");
        if (t is not null) parts.Add($"t={t:O}");
        return string.Join("; ", parts);
    }
}
