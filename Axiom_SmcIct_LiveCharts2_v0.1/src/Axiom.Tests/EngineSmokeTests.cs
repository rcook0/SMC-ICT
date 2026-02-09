using Axiom.Core;
using Axiom.SmcIct;

namespace Axiom.Tests;

public sealed class EngineSmokeTests
{
    [Xunit.Fact]
    public void Analyze_Returns_Stable_Contract()
    {
        var candles = new List<Candle>();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        double p = 100;

        for (int i = 0; i < 200; i++)
        {
            var t = t0.AddMinutes(5 * i);
            var o = p;
            var c = p + (i % 7 == 0 ? 0.3 : -0.1);
            var hi = Math.Max(o, c) + 0.2;
            var lo = Math.Min(o, c) - 0.2;
            candles.Add(new Candle(t, o, hi, lo, c, 1));
            p = c;
        }

        var eng = new SmcIctEngine();
        var res = eng.Analyze(candles);

        Xunit.Assert.NotNull(res);
        Xunit.Assert.NotNull(res.Zones);
        Xunit.Assert.NotNull(res.Signals);
    }
}
