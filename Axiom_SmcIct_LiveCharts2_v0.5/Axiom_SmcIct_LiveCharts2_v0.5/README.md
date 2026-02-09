# Axiom.SmcIct — LiveCharts2 + Backtest skeleton v0.5

This is the **v0.5** iteration of the generalized SMC/ICT engine + viewer + exporter, focusing on the **trade model** layer:
SL/TP rules, liquidity target pools, and a simple trade simulator (partials + BE).

## What’s new in v0.5 (Trade model)

### Signal hints now include a real trade model
Signals (`SignalEvent`) now populate:
- `EntryHint` = mitigation close (entry candle close on entry series)
- `SlHint` = zone invalidation ± `SlBufferTicks`
- `Tp1Hint`/`Tp2Hint` = liquidity pool targets (or RR fallback)

### Liquidity target pools (TP model = `LiquidityPools`)
Targets are selected from:
- External dealing range extremes: `EXT_HIGH`, `EXT_LOW`
- EQ pools from detected `EQH`/`EQL` zones
- Previous day high/low: `PDH`, `PDL` (enabled by default)

Fallback: `DefaultRR` (RR multiple) when no pool is found.

### Partial + Break-even simulation (Backtest)
`Axiom.Backtest` now produces:
- `trades.json` / `trades.csv`
- summary stats (`summary.json`)

Simulation model (deterministic, minimal):
- Enter at `EntryHint`
- Exit logic:
  - Stop at `SlHint`
  - Partial at `Tp1Hint` (50% by default), move SL to BE
  - Exit remainder at `Tp2Hint` (or stop at BE after partial)

> Economics (spread/slippage/commission) is v0.6+. v0.5 focuses on the **trade model semantics**.

## Build
```bash
dotnet build Axiom.SmcIct.sln
```

## Run backtest/export
```bash
dotnet run --project src/Axiom.Backtest -- --csv data/sample_ohlcv.csv --out out
```

Outputs:
- `out/zones.json`, `out/signals.json`
- `out/trades.json`, `out/trades.csv`
- `out/summary.json`

## Run WPF chart app (Windows)
```bash
dotnet run --project src/Axiom.App
```

See `docs/GUIDE.md` for rule semantics and `docs/ROADMAP.md` for next steps.


## Headless build (Linux/macOS)

If you don't want the WPF chart app, build the headless solution:

```bash
dotnet build Axiom.SmcIct.Headless.sln
```
