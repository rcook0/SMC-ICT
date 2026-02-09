# ROADMAP (v0.1 → v1)

## v0.1 (this workspace)
- Contracts stabilized (`Candle`, `Zone`, `SignalEvent`)
- Stub strategy pipeline emits synthetic zones/signals
- Backtest CLI + JSON/CSV outputs
- WPF LiveCharts2 viewer with overlays

## v0.2 (core correctness)
- Implement pivot-based swings on selectable TF
- Implement BOS/CHoCH + internal/external structure
- Implement EQH/EQL pools (tolerance + clustering)
- Add displacement qualification (body/ATR + break)

## v0.3 (zones + lifecycle)
- OB: "last opposite candle before displacement"
- FVG: only during displacement, midline entry option
- Breaker: role flip after structural break
- Zone lifecycle: active/inactive, mitigated, invalidated

## v0.4 (trade model)
- Entry models: OB, FVG50, Breaker, OTE, Any(score)
- Risk/SL: structural invalidation
- Targets: nearest liquidity pools, ladder TP1/TP2/TP3
- BE + partials

## v0.5 (research grade)
- Parameter sweeps + report packs
- Determinism + golden vectors
- Fast replay UI (bar-by-bar with reasons)

## v1.0
- Production-ready strategy core + backtest economics
- UI presets + export to evidence pack
