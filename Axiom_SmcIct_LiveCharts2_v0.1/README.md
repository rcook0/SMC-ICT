# Axiom.SmcIct — .NET solution skeleton (LiveCharts2) v0.1

This workspace is a **compile-ready skeleton** for a generalized SMC/ICT strategy stack, designed so the strategy core can evolve while
UI/backtesting stay stable.

## What’s included

- **Axiom.Core**: contracts + small utilities (portable).
- **Axiom.SmcIct**: stub strategy pipeline that emits `Zone` + `SignalEvent` (portable).
- **Axiom.Backtest**: CLI runner that loads candles from CSV, runs the engine, and writes outputs (portable).
- **Axiom.App** (**WPF + LiveCharts2**): chart viewer that overlays signals + zones (Windows).
- **Axiom.Tests**: minimal xUnit smoke tests.

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

The app loads `data/sample_ohlcv.csv` and overlays stub zones/signals.

## Next steps (roadmap)

See `docs/ROADMAP.md`.
