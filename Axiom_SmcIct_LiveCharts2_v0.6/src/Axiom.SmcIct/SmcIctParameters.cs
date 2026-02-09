namespace Axiom.SmcIct;

public sealed record SmcIctParameters(
    // Structure
    int InternalSwingLen = 3,
    int ExternalSwingLen = 7,
    bool UseWickForStructure = false,
    bool DeterministicSwings = true,   // v0.6: confirmed swings only (no lookahead)

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
    string EntryModel = "Any",           // Any | OB | FVG50 | Breaker | OTE
    bool RequireMicroConfirm = false,

    // v0.5+ Trade model
    double SlBufferTicks = 5.0,          // SL buffer beyond invalidation (in ticks)
    string TpModel = "LiquidityPools",   // LiquidityPools | RR
    double DefaultRR = 2.0,              // used when TpModel=RR or when no pools are found
    bool EmitTp2 = true,

    // TP pool modules
    bool UseExtRange = true,
    bool UseEqPools = true,
    bool UsePDH_PDL = true,
    bool UsePWH_PWL = true,              // v0.6
    bool UseSessionHiLo = true,          // v0.6 (now implemented)
    string SessionPreset = "ICT_UTC",    // v0.6: ICT_UTC | FX_UTC

    // Context filters
    bool RequireDiscountPremium = false, // longs only in discount, shorts only in premium (based on ext dealing range)

    // OTE bounds (if EntryModel=OTE)
    double OteLow = 0.62,
    double OteHigh = 0.79
);
