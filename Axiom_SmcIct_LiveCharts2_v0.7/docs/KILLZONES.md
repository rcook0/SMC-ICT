# Killzones (v0.7)

This project treats "killzones" as optional **signal windows** in UTC.

Why:
- Many ICT traders focus on high-liquidity windows (London / New York) where displacement and raid/return patterns are more frequent.
- In automation, restricting entries can reduce low-quality signals in dead hours (but can also miss trends).

## Presets

### ICT_KZ_UTC (default preset if you enable gating)
- LONDON_KZ: 07:00–10:00 UTC
- NY_KZ:     12:00–15:00 UTC

These are *UTC approximations*. If you want DST-aware handling, do it at the data adapter layer (v0.8+ idea).

## Parameters
- `RestrictToKillzones` (bool)
- `KillzonePreset` (string) – currently `ICT_KZ_UTC`

## Behavior
If enabled, **signals are only emitted** when the mitigation candle timestamp falls in any killzone window.
