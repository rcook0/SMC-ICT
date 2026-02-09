# Axiom.SmcIct — .NET solution skeleton (LiveCharts2) v0.6

v0.6 focuses on **broker-grade economics**, **determinism / no-lookahead**, and **extensible TP pool modules**.
The SMC/ICT model continues to emit zones + trade-ready signals, but now trade simulation can be made realistic.

## v0.6 adds

### 1) Economics (spread / slippage / commission)
- Entry/exit fills are now adjusted by:
  - **Spread** (ticks or bps, symmetric half-spread)
  - **Slippage** (ticks or bps, always adverse)
  - **Commission** (bps of notional or fixed per side)
- Trade simulator computes **net R-multiple** after costs.

### 2) Determinism (no lookahead)
- Swings are now **confirmed** with a `len`-bar delay:
  - A pivot at index `i` becomes available at `i + len`
- Engine consumes only swings whose **confirm index** is <= current bar index.
- Added tests to assert stable outputs across repeated runs and to detect lookahead.

### 3) TP pool modules
`TpModel=LiquidityPools` can draw targets from these modules (toggle individually):
- `UsePDH_PDL` — previous day high/low
- `UsePWH_PWL` — previous week high/low
- `UseSessionHiLo` — session high/low via preset windows
- `UseEqPools` — EQH/EQL pools (already)
- `UseExtRange` — external range extremes (already)

### 4) OTE (entry refinement) implemented
`EntryModel=OTE` requires mitigation entry to occur inside the **OTE retracement band**
(see Guide section below).

## Run backtest
```bash
dotnet run --project src/Axiom.Backtest -- --csv data/sample_ohlcv.csv --out out ^
  --spread_ticks 20 --slip_ticks 5 --comm_bps 2.5
```

Other useful flags:
- `--partial 0.5`
- `--be 1`
- `--max_hold_bars 0`

## OTE quick guide
OTE (Optimal Trade Entry) = an ICT entry refinement that seeks entries in a deep retracement of a
displacement leg.

Given a dealing range `[L, H]` (low-to-high for bullish context):
- Range = `H - L`
- Bullish OTE band (discount): `[H - 0.79*Range, H - 0.62*Range]`
- Bearish OTE band (premium): `[L + 0.62*Range, L + 0.79*Range]`

In this repo the dealing range defaults to the **current external structure extremes**
(confirmed swings), and OTE is applied as a filter on zone mitigation entries.

See `docs/OTE.md`.
