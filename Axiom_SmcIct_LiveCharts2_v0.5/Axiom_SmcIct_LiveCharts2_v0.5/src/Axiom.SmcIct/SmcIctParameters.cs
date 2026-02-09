namespace Axiom.SmcIct;

public sealed record SmcIctParameters(
    // Structure
    int InternalSwingLen = 3,
    int ExternalSwingLen = 7,
    bool UseWickForStructure = false,

    // Liquidity / EQ
    double EqTolerance = 0.0,            // price units; 0 => auto via tick size estimate
    double EqClusterMinSeparationTicks = 25, // avoid spamming EQ levels too close (in ticks; uses estimated tick)
    int SweepLookbackBars = 80,
    double SweepToleranceTicks = 10.0,   // sweep threshold in ticks (tick estimated)
    bool RequireInducement = false,
    int InducementLookbackBars = 40,

    // Displacement
    double DisplacementFactor = 1.5,     // body > factor * SMA(body)
    int DisplacementSmaLen = 20,

    // Zones
    int ObLookbackBars = 6,
    bool ObUseBody = true,
    bool FvgRequireDisplacement = true,
    int ZoneMaxAgeBars = 400,            // expire zones after this many entry bars (0 => never)

    // Entries
    string EntryModel = "Any",           // Any | OB | FVG50 | Breaker | OTE (OTE not implemented yet)
    bool RequireMicroConfirm = false,

    // v0.5 Trade model
    double SlBufferTicks = 5.0,          // SL buffer beyond invalidation (in ticks)
    string TpModel = "LiquidityPools",   // LiquidityPools | RR
    double DefaultRR = 2.0,              // used when TpModel=RR or when no pools are found
    bool EmitTp2 = true,
    bool UsePDH_PDL = true,
    bool UseSessionHiLo = false,         // scaffold: disabled by default
    bool RequireDiscountPremium = false  // if true: longs only in discount, shorts only in premium (based on external dealing range)
);
