using System.Text.Json.Serialization;

namespace Axiom.Core;

public readonly record struct Candle(
    DateTime Time,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume = 0);

public enum Side { Buy, Sell }

public enum ZoneType
{
    OrderBlock,
    FairValueGap,
    Breaker,
    EQH,
    EQL,
    DealingRange,
}

public readonly record struct Zone(
    Guid Id,
    ZoneType Type,
    string Tf,
    DateTime Created,
    DateTime? Expires,
    double Low,
    double High,
    bool IsBullish,
    string? Meta = null);

public readonly record struct SignalEvent(
    DateTime Time,
    Side Side,
    Guid? ZoneId,
    double? EntryHint,
    double? SlHint,
    double? Tp1Hint,
    double? Tp2Hint,
    IReadOnlyList<string> Reasons);


public readonly record struct TradeResult(
    DateTime EntryTime,
    DateTime ExitTime,
    Side Side,
    double Entry,
    double Exit,
    double InitialSL,
    double? Tp1,
    double? Tp2,
    string ExitReason,
    double RMultiple,
    Guid? ZoneId,
    IReadOnlyList<string> Reasons);

public sealed record AnalysisResult(
    IReadOnlyList<Zone> Zones,
    IReadOnlyList<SignalEvent> Signals);

public static class Timeframes
{
    public const string H4 = "H4";
    public const string H1 = "H1";
    public const string M15 = "M15";
    public const string M5 = "M5";
}
