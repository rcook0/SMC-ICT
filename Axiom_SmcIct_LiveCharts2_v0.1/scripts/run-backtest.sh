#!/usr/bin/env bash
set -euo pipefail
dotnet run --project src/Axiom.Backtest -- --csv data/sample_ohlcv.csv --out out
