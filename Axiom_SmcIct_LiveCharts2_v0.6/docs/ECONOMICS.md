# Economics (spread / slippage / commission)

This backtester treats the candle prices as a **mid** reference and applies a simple execution model:

- **Entry fill**
  - Buy: `mid + spread/2 + slippage`
  - Sell: `mid - spread/2 - slippage`

- **Exit fill**
  - Closing Buy (sell): `mid - spread/2 - slippage`
  - Closing Sell (buy): `mid + spread/2 + slippage`

Slippage is always adverse.

Commission is applied per executed side:
- `commission = notional * comm_bps/10000 + comm_fixed`
- Notional is approximated as `fill_price * quantity`.

## CLI flags

- `--spread_ticks <double>`
- `--spread_bps <double>`
- `--slip_ticks <double>`
- `--slip_bps <double>`
- `--comm_bps <double>`
- `--comm_fixed <double>`
- `--qty <double>`
