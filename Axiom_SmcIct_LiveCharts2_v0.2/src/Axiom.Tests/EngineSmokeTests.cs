using Axiom.Core;
using Axiom.SmcIct;

namespace Axiom.Tests;

public sealed class EngineSmokeTests
{
    [Xunit.Fact]
    public void Analyze_Emits_Structure_Primitives()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_ohlcv.csv");
        var candles = CandleCsv.Load(path);

        var eng = new SmcIctEngine(new SmcIctParameters(SwingLenLtf: 3));
        var res = eng.Analyze(candles, tf: Timeframes.M5);

        Xunit.Assert.True(res.Zones.Count > 0, "Expected at least 1 zone (EQH/EQL).");
        Xunit.Assert.True(res.Signals.Count > 0, "Expected at least 1 CHoCH-derived signal.");
    }
}
