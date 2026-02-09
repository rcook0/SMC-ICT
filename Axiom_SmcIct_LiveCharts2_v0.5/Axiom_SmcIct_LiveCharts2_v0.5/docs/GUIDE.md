# Guide — v0.5 Trade Model Semantics

This guide is intentionally “single-page”: it describes the **SL/TP model** and the **backtest simulation**.

## 1) Entry

A signal is emitted when price **mitigates** a qualifying zone and closes with directional bias.

- `EntryHint` = close of the entry candle at the mitigation bar (entry series)
- Optional `RequireMicroConfirm` (if input TF is M5): last M5 candle in the M15 bucket must agree with direction.

## 2) Stop Loss (SL)

**Primary rule:** *zone invalidation* with buffer.

- Buy: `SL = zone.low - SlBufferTicks * tick`
- Sell: `SL = zone.high + SlBufferTicks * tick`

Fallback if no zone: external dealing range extremes.

## 3) Take Profit model (TP)

Default `TpModel=LiquidityPools`.

Pools considered:
- External dealing range extremes: EXT_HIGH / EXT_LOW
- EQH/EQL zones (liquidity pools)
- Previous day high/low: PDH/PDL

**TP1** = nearest pool beyond entry in the trade direction.
**TP2** = next pool (if available) else TP1 ± 1R.

Fallback: RR model `DefaultRR` when no pool exists beyond entry.

## 4) Partial + Break-even simulation

In `Axiom.Backtest`:
- Enter at `EntryHint`
- If SL hit before TP1 → full stop.
- If TP1 hit:
  - Close 50% (hardcoded in v0.5)
  - Move SL to BE (entry)
- Then:
  - Exit remaining at TP2, or at BE if price reverses.

The simulator reports `RMultiple` (P/L divided by initial risk).

## 5) Notes

- Structure primitives still use pivot swings (lookahead). v0.6 introduces delayed confirmation fixtures to eliminate lookahead.
- Session highs/lows are scaffolded but disabled by default to avoid hidden timezone assumptions.
