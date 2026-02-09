using Axiom.Core;
using Axiom.SmcIct;

namespace Axiom.Tests;

public sealed class EngineSmokeTests
{
    [Xunit.Fact]
    public void Analyze_Emits_Zones_And_Signals_V05()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_ohlcv.csv");
        var candles = CandleCsv.Load(path);

        var eng = new SmcIctEngine(new SmcIctParameters(
            InternalSwingLen: 3,
            ExternalSwingLen: 7,
            DisplacementFactor: 1.2,
            RequireInducement: false,
            EntryModel: "Any"
        ));

        var res = eng.Analyze(candles, tf: Timeframes.M5);

        Xunit.Assert.True(res.Zones.Count > 0, "Expected zones.");
        Xunit.Assert.True(res.Signals.Count > 0, "Expected signals.");
        Xunit.Assert.Contains(res.Zones, z => z.Type is ZoneType.OrderBlock or ZoneType.FairValueGap or ZoneType.Breaker or ZoneType.EQH or ZoneType.EQL);
        Xunit.Assert.Contains(res.Signals, s => s.SlHint is not null && s.Tp1Hint is not null);
    }
}
