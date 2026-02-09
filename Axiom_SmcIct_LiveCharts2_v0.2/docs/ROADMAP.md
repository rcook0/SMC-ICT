# ROADMAP (v0.2 → v1)

## v0.2 (this workspace)
- Contracts stabilized (`Candle`, `Zone`, `SignalEvent`)
- Implemented:
  - Pivot swings (Entry TF)
  - BOS / CHoCH (trend-state aware)
  - EQH / EQL liquidity pools (tolerance-based)
  - M5 → M15 resampler
- Backtest CLI + JSON/CSV outputs
- WPF LiveCharts2 viewer with overlays

## v0.3 (displacement + zones)
- Displacement qualification (body/ATR + break)
- OB: "last opposite candle before displacement"
- FVG: only during displacement, plus midline entry option
- Breaker: role flip after structural break
- Zone lifecycle: active/inactive, mitigated, invalidated

## v0.4 (trade model)
- Entry models: OB, FVG50, Breaker, OTE, Any(score)
- SL: structural invalidation
- Targets: nearest liquidity pools, ladder TP1/TP2/TP3
- BE + partials

## v0.5 (research grade)
- Parameter sweeps + report packs
- Determinism + golden vectors (hash fixtures)
- Fast replay UI (bar-by-bar with reasons)

## v1.0
- Production-ready strategy core + backtest economics
- UI presets + export to evidence pack
