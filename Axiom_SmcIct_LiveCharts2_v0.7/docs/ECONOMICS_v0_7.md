# Economics v0.7 — ATR-aware execution mode

v0.6 supported fixed spread/slippage/commission.

v0.7 adds `Mode=Atr` where spread/slippage can increase with volatility.

## Config
- `Mode`: `Fixed` or `Atr`
- `AtrLen`: ATR lookback (default 14)
- `AtrSpreadFrac`: spread += frac * ATR
- `AtrSlipFrac`:   slippage += frac * ATR

Example:
- ATR ~ 40 points, `AtrSpreadFrac=0.02` -> add 0.8 points to spread.

CLI:
```bash
dotnet run --project src/Axiom.Backtest -- --csv data/sample_ohlcv.csv --out out ^
  --econo_mode Atr --atr_len 14 --atr_spread_frac 0.02 --atr_slip_frac 0.01
```
