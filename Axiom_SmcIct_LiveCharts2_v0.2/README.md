# Axiom.SmcIct — .NET solution skeleton (LiveCharts2) v0.2

This workspace is a **compile-ready skeleton** for a generalized SMC/ICT strategy stack, designed so the strategy core can evolve while
UI/backtesting stay stable.

## What’s new in v0.2

- Replaced the stub SMA-cross-only logic with real **structure primitives**:
  - **Pivot swings** (Entry TF series)
  - **BOS / CHoCH** (trend-state aware)
  - **EQH / EQL** liquidity pools (tolerance-based, lightweight clustering)
- Added a small **M5 → M15 resampler** (so you can feed M5 and do structure on M15).
- Moved CSV loader into `Axiom.Core` so both Backtest + App can load candles cleanly.
- Added deterministic-ish tests (smoke + “outputs are non-empty”).

## Projects

- **Axiom.Core**: contracts + utilities + `CandleCsv`.
- **Axiom.SmcIct**: SMC/ICT engine (v0.2: swings/BOS/CHoCH/EQ).
- **Axiom.Backtest**: CLI runner for CSV → zones/signals outputs.
- **Axiom.App** (**WPF + LiveCharts2**): chart viewer overlaying signals + zones.
- **Axiom.Tests**: xUnit.

> Note: WPF is Windows-only. Core/SmcIct/Backtest/Tests are cross-platform.

## Prereqs

- .NET SDK **8.0+**
- Windows for **Axiom.App** (WPF)

## Build

```bash
dotnet build Axiom.SmcIct.sln
```

## Run backtest (console)

```bash
dotnet run --project src/Axiom.Backtest -- --csv data/sample_ohlcv.csv --out out
```

Outputs:
- `out/signals.json`
- `out/zones.json`
- `out/signals.csv`
- `out/zones.csv`

## Run chart app (WPF)

From Windows:

```bash
dotnet run --project src/Axiom.App
```

The app loads `data/sample_ohlcv.csv` and overlays zones/signals.

## Next steps

See `docs/ROADMAP.md`.
