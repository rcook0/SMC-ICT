namespace Axiom.SmcIct;

public sealed record SmcIctParameters(
    int SwingLenHtf = 3,
    int SwingLenLtf = 3,
    double EqTolerance = 0.0,      // price units; 0 => auto via tick size in adapters
    double DisplacementFactor = 1.5,
    string EntryModel = "Any"      // Any | OB | FVG50 | Breaker | OTE
);
