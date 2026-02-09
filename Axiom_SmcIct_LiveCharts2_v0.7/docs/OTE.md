# OTE (Optimal Trade Entry) — how we implement it

OTE is an ICT refinement that attempts to enter after a displacement leg, on a deep retracement into
a high-probability band, typically associated with 0.62–0.79 Fibonacci retracement (from the impulse extreme).

## Definitions

Let a dealing range be `[L, H]`:

- Range `R = H - L`

### Bullish context (buy)
OTE band is defined from the *impulse high* retracing downward:
- `OTE_Low  = H - 0.79 * R`
- `OTE_High = H - 0.62 * R`

Entry is valid if price is inside `[OTE_Low, OTE_High]`.

### Bearish context (sell)
OTE band is defined from the *impulse low* retracing upward:
- `OTE_Low  = L + 0.62 * R`
- `OTE_High = L + 0.79 * R`

Entry is valid if price is inside `[OTE_Low, OTE_High]`.

## Anchors used here

We anchor `[L, H]` to **external structure extremes** (confirmed swings) at the time of entry.
This makes it deterministic and avoids hidden assumptions.

## How it interacts with zones

OTE is applied as an additional filter on **zone mitigation entries**:
- A mitigation touch must occur AND the entry price must be inside the OTE band.

This is intentionally conservative; you can relax it by:
- Using OTE only for OB/FVG, not for breakers
- Widening band bounds
- Anchoring to “displacement leg” endpoints rather than external extremes
